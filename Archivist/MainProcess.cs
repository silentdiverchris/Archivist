using Archivist.Classes;
using Archivist.Helpers;
using Archivist.Models;
using Archivist.Services;
using Archivist.Utilities;
using System.Diagnostics;
using static Archivist.Enumerations;

namespace Archivist
{
    internal class MainProcess : IDisposable
    {
        private readonly JobDetails _jobDetails;
        private readonly LogService _logService;

        static readonly object _lock = new();

        internal MainProcess(JobDetails jobDetails)
        {
            try
            {
                _jobDetails = jobDetails;

                _logService = new LogService(
                    jobDetails: jobDetails,
                    consoleDelegate: new(WriteToConsole));
            }
            catch (Exception ex)
            {
                EventLogHelpers.WriteEntry($"MainProcess constructor exception: {ex.Message} {ex.Source}", enSeverity.Error);
                throw;
            }
        }

        internal async Task<Result> Initialise()
        {
            Result result = new("MainProcess.Initialise");

            DateTime startTime = _jobDetails.AppSettings.UseUtcTime ? DateTime.UtcNow : DateTime.Now;
            string utcIndicator = _jobDetails.AppSettings.UseUtcTime ? "UTC" : "local time";
            result.AddInfo($"Archivist starting at {startTime.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} {utcIndicator}");

            if (_logService.LoggingToFile)
            {
                result.AddInfo($"Log file '{_logService.LogFileName}'");
            }
            else
            {
                result.AddInfo($"Logging to file not enabled");
            }

            if (_logService.LoggingToSql)
            {
                result.AddInfo($"Logging to SQL database '{_logService.LoggingToSqlDatabaseName}'");
            }
            else
            {
                result.AddInfo($"Logging to SQL not enabled");
            }

            result.SubsumeResult(AppSettingsUtilities.SelectAndValidateJob(_jobDetails));

            if (result.HasNoErrors)
            {
                EventLogHelpers.WriteEntry($"Initialised job '{_jobDetails.JobNameToRun}'", enSeverity.Info);
            }
            else
            {
                result.AddError($"One or more errors found initialising job");
            }

            await _logService.ProcessResult(result);

            return result;
        }

        internal async Task<Result> RunAsync()
        {
            Result result = new("Archivist", true, $"job '{_jobDetails.SelectedJob.Name}'");

            await _logService.ProcessResult(result);

            if (_jobDetails.SelectedJob.AutoViewLogFile && _logService.LoggingToFile)
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo(_logService.LogFileName)
                    {
                        UseShellExecute = true
                    }
                };
                proc.Start();
            }

            RecordStatistics(true);

            if (_jobDetails.AppSettings.AESEncryptPath is not null)
            {
                using (var secureDirectoryService = new SecureDirectoryService(_jobDetails.SelectedJob, _logService, _jobDetails.AppSettings.AESEncryptPath))
                {
                    Result secureDirectoryResult = await secureDirectoryService.ProcessSecureDirectories();
                    result.SubsumeResult(secureDirectoryResult);
                }
            }

            if (!result.HasErrors)
            {
                using (var compressionService = new CompressionService(_jobDetails.SelectedJob, _logService, _jobDetails.AppSettings.AESEncryptPath))
                {
                    Result compressionResult = await compressionService.CompressSources();
                    result.SubsumeResult(compressionResult);
                }

                if (!result.HasErrors)
                {
                    using (var fileService = new FileService(_jobDetails.SelectedJob, _logService))
                    {
                        Result copyArchivesResult = await fileService.CopyToArchives();
                        result.SubsumeResult(copyArchivesResult);

                        Result reportResult = await fileService.GenerateFileReport();
                        result.SubsumeResult(reportResult);
                    }
                }
            }

            RecordStatistics(false);

            await _logService.ProcessResult(result, reportAllStatistics: true);

            _jobDetails.EndedUtc = DateTime.UtcNow;

            if (result.HasErrors)
            {
                EventLogHelpers.WriteEntry($"Archivist completed job {_jobDetails.JobNameToRun} with errors", enSeverity.Error);
                result.AddError($"Job {_jobDetails.JobNameToRun} completed with errors");
            }
            else if (result.HasWarnings)
            {
                EventLogHelpers.WriteEntry($"Archivist completed job {_jobDetails.JobNameToRun} with warnings", enSeverity.Warning);
                result.AddWarning($"Job {_jobDetails.JobNameToRun} completed with warnings");
            }
            else
            {
                EventLogHelpers.WriteEntry($"Archivist completed job {_jobDetails.JobNameToRun} successfully", enSeverity.Info);
                result.AddSuccess($"Job {_jobDetails.JobNameToRun} completed successfully");
            }

            await ReportAllDiskSpaceRemaining();

            await _logService.ProcessResult(result);

            if (_jobDetails.SelectedJob.PauseBeforeExit)
            {
                Console.WriteLine("Press any key to exit");
                Console.ReadLine();
            }

            return result;
        }

        /// <summary>
        /// Report the space remaining on all disks we used
        /// </summary>
        internal async Task ReportAllDiskSpaceRemaining()
        {
            Result result = new("ReportAllDiskSpaceRemaining");

            result.SubsumeResult(FileUtilities.CheckDiskSpace(_jobDetails.SelectedJob.PrimaryArchiveDirectoryName));

            foreach (var dir in _jobDetails.SelectedJob.ArchiveDirectories.Where(_ => _.IsEnabled && _.IsAvailable).OrderBy(_ => _.DirectoryPath))
            {
                dir.VerifyVolume(); 
                result.SubsumeResult(FileUtilities.CheckDiskSpace(dir.DirectoryPath, dir.VolumeLabel));
            }

            await _logService.ProcessResult(result);
        }

        internal void RecordStatistics(bool initial)
        {
            Result result = new("RecordDiskStatistics");

            if (initial)
            {
                _jobDetails.SelectedJob.PrimaryArchiveStatistics.BytesFreeInitial = FileUtilities.GetAvailableDiskSpace(_jobDetails.SelectedJob.PrimaryArchiveDirectoryName);
            }
            else
            {
                _jobDetails.SelectedJob.PrimaryArchiveStatistics.BytesFreeFinal += FileUtilities.GetAvailableDiskSpace(_jobDetails.SelectedJob.PrimaryArchiveDirectoryName);
            }

            foreach (var dir in _jobDetails.SelectedJob.ArchiveDirectories.Where(_ => _.IsEnabled && _.IsAvailable))
            {
                //dir.VerifyVolume();

                if (initial)
                {
                    dir.Statistics.BytesFreeInitial = FileUtilities.GetAvailableDiskSpace(dir.DirectoryPath);
                }
                else
                {
                    dir.Statistics.BytesFreeFinal += FileUtilities.GetAvailableDiskSpace(dir.DirectoryPath);
                }
            }
        }

        internal void WriteToConsole(LogEntry entry)
        {
            if (_jobDetails.WriteToConsole)
            {
                if (entry.PercentComplete is not null)
                {
                    lock (_lock)
                    {
                        ConsoleUtilities.WriteProgressBar(
                            percentComplete: (short)entry.PercentComplete,
                            prefix: entry.ProgressPrefix,
                            suffix: entry.ProgressSuffix);
                    }
                }
                else
                {
                    if (entry.Severity == enSeverity.Warning)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    }
                    else if (entry.Severity == enSeverity.Error)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    else if (entry.Severity == enSeverity.Success)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                    }

                    Console.WriteLine(entry.Text);

                    if (entry.Severity != enSeverity.Info)
                        Console.ResetColor();
                }
            }
        }

        public void Dispose()
        {
            if (_logService is not null)
                _logService.Dispose();
        }

        private static Result ValidateJobDetails(JobDetails jobDetails)
        {
            Result result = new("ValidateJobDetails", false);

            //if (jobDetails.ConfigFilePath is null)
            //{
            //    result.AddError($"Config file name is not supplied in AppSettings.json 'ConfigurationFile'");
            //}
            //else if (!File.Exists(jobDetails.ConfigFilePath))
            //{
            //    result.AddError($"Configuration file '{jobDetails.ConfigFilePath}' does not exist");
            //}

            if (!string.IsNullOrEmpty(jobDetails.AppSettings.AESEncryptPath) && !File.Exists(jobDetails.AppSettings.AESEncryptPath))
            {
                result.AddError($"AESEncrypt executable '{jobDetails.AppSettings.AESEncryptPath}' does not exist, please download from https://www.aescrypt.com/");
            }

            if (!string.IsNullOrEmpty(jobDetails.AppSettings.LogDirectoryPath) && !Directory.Exists(jobDetails.AppSettings.LogDirectoryPath))
            {
                result.AddError($"Log directory '{jobDetails.AppSettings.LogDirectoryPath}' does not exist");
            }

            if (jobDetails.JobNameToRun is null)
            {
                result.AddError($"RunJobName is not supplied as parameter 1 or found in appsettings.json 'RunJobName'");
            }

            return result;
        }


    }
}
