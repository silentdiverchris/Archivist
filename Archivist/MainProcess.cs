using Archivist.Classes;
using Archivist.Helpers;
using Archivist.Models;
using Archivist.Services;
using Archivist.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Archivist.Enumerations;

namespace Archivist
{
    internal class MainProcess : IDisposable
    {
        private readonly JobDetails _jobDetails;
        private readonly LogService _logService;
        private readonly AppSettings _appSettings;

        static readonly object _lock = new();

        internal MainProcess(JobDetails jobDetails, AppSettings appSettings)
        {
            try
            {
                _jobDetails = jobDetails;
                _appSettings = appSettings;

                _logService = new LogService(
                    jobDetails: jobDetails,
                    appSettings: appSettings,
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

            DateTime startTime = _appSettings.UseUtcTime ? DateTime.UtcNow : DateTime.Now;
            string utcIndicator = _appSettings.UseUtcTime ? "UTC" : "local time";
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

            result.SubsumeResult(AppSettingsUtilities.SelectAndValidateJob(_jobDetails, _appSettings));

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
            Result result = new("Archivist", true, $"job '{_appSettings.SelectedJob?.Name}'");

            await _logService.ProcessResult(result);

            if (_appSettings.SelectedJob is null)
            {
                throw new Exception("RunAsync called without a selected job, this shouldn't be possible");
            }

            if (_logService.LoggingToFile && (_appSettings.SelectedJob!.AutoViewLogFile))
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo(_logService.LogFileName!)
                    {
                        UseShellExecute = true
                    }
                };
                proc.Start();
            }

            RecordStatistics(true);

            // instantiate one here just for testign to precded any compressioning
            //var archiveRegistry = new ArchiveRegistry(_appSettings.SelectedJob!.PrimaryArchiveDirectoryPath!, _appSettings.SelectedJob.SourceDirectories, _appSettings.SelectedJob.ArchiveDirectories);
            //_logService.DumpArchiveRegistry(archiveRegistry);

            if (_appSettings.AESEncryptPath is not null)
            {
                using (var secureDirectoryService = new SecureDirectoryService(_appSettings.SelectedJob, _appSettings, _logService, _appSettings.AESEncryptPath))
                {
                    Result secureDirectoryResult = await secureDirectoryService.ProcessSecureDirectories();
                    result.SubsumeResult(secureDirectoryResult);
                }
            }

            if (!result.HasErrors)
            {
                using (var compressionService = new CompressionService(_appSettings.SelectedJob, _appSettings, _logService, _appSettings.AESEncryptPath!))
                {
                    Result compressionResult = await compressionService.CompressSources();
                    result.SubsumeResult(compressionResult);

                    // To be implemented soon
                    //var archiveRegister = new ArchiveRegister(_appSettings.SelectedJob!.PrimaryArchiveDirectoryPath!, _appSettings.SelectedJob.SourceDirectories, _appSettings.SelectedJob.ArchiveDirectories);
                    //_logService.DumpArchiveRegistry(archiveRegister);
                    //Result executeResult = await fileService.ExecuteFileCompressActions(archiveRegister);
                    //result.SubsumeResult(executeResult);
                }

                if (!result.HasErrors)
                {
                    var archiveRegister = new ArchiveRegister(_appSettings.SelectedJob!.PrimaryArchiveDirectoryPath!, _appSettings.SelectedJob.SourceDirectories, _appSettings.SelectedJob.ArchiveDirectories);
                    _logService.DumpArchiveRegistry(archiveRegister);

                    using (var fileService = new FileService(_appSettings.SelectedJob, _appSettings, _logService))
                    {
                        Result executeResult = await fileService.ExecuteFileCopyActions(archiveRegister);
                        result.SubsumeResult(executeResult);

                        // To be implemented soon
                        //var archiveRegister = new ArchiveRegister(_appSettings.SelectedJob!.PrimaryArchiveDirectoryPath!, _appSettings.SelectedJob.SourceDirectories, _appSettings.SelectedJob.ArchiveDirectories);
                        //_logService.DumpArchiveRegistry(archiveRegister);
                        //Result executeResult = await fileService.ExecuteFileDeleteActions(archiveRegister);
                        //result.SubsumeResult(executeResult);

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
                result.AddError($"Job {_jobDetails.JobNameToRun} completed with errors", consoleBlankLineBefore: true, consoleBlankLineAfter: true);
            }
            else if (result.HasWarnings)
            {
                EventLogHelpers.WriteEntry($"Archivist completed job {_jobDetails.JobNameToRun} with warnings", enSeverity.Warning);
                result.AddWarning($"Job {_jobDetails.JobNameToRun} completed with warnings", consoleBlankLineBefore: true, consoleBlankLineAfter: true);
            }
            else
            {
                EventLogHelpers.WriteEntry($"Archivist completed job {_jobDetails.JobNameToRun} successfully", enSeverity.Info);
                result.AddSuccess($"Job {_jobDetails.JobNameToRun} completed successfully", consoleBlankLineBefore: true, consoleBlankLineAfter: true);
            }

            await ReportAllDiskSpaceRemaining();

            await _logService.ProcessResult(result);

            if (_appSettings.SelectedJob.PauseBeforeExit)
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

            result.SubsumeResult(FileUtilities.CheckDiskSpace(_appSettings.SelectedJob!.PrimaryArchiveDirectoryPath!));

            foreach (var dir in _appSettings.SelectedJob.ArchiveDirectories.Where(_ => _.IsEnabled && _.IsAvailable).OrderBy(_ => _.DirectoryPath))
            {
                dir.VerifyVolume();
                result.SubsumeResult(FileUtilities.CheckDiskSpace(dir.DirectoryPath!, dir.VolumeLabel));
            }

            await _logService.ProcessResult(result);
        }

        internal void RecordStatistics(bool initial)
        {
            Result result = new("RecordDiskStatistics");

            if (initial)
            {
                _appSettings.SelectedJob!.PrimaryArchiveStatistics.BytesFreeInitial = FileUtilities.GetAvailableDiskSpace(_appSettings.SelectedJob!.PrimaryArchiveDirectoryPath!);
            }
            else
            {
                _appSettings.SelectedJob!.PrimaryArchiveStatistics.BytesFreeFinal += FileUtilities.GetAvailableDiskSpace(_appSettings.SelectedJob!.PrimaryArchiveDirectoryPath!);
            }

            foreach (var dir in _appSettings.SelectedJob.ArchiveDirectories.Where(_ => _.IsEnabled && _.IsAvailable))
            {
                //dir.VerifyVolume();

                if (initial)
                {
                    dir.Statistics.BytesFreeInitial = FileUtilities.GetAvailableDiskSpace(dir.DirectoryPath!);
                }
                else
                {
                    dir.Statistics.BytesFreeFinal += FileUtilities.GetAvailableDiskSpace(dir.DirectoryPath!);
                }
            }
        }

        internal void WriteToConsole(LogEntry entry)
        {
            if (_appSettings.SelectedJob?.WriteToConsole ?? true) // If in doubt, log to the console
            {
                if (entry.ConsoleBlankLineBefore)
                {
                    Console.WriteLine();
                }

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

                if (entry.ConsoleBlankLineAfter)
                {
                    Console.WriteLine();
                }
            }
        }

        public void Dispose()
        {
            if (_logService is not null)
                _logService.Dispose();
        }

        private static Result ValidateJobDetails(JobDetails jobDetails, AppSettings appSettings)
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

            if (!string.IsNullOrEmpty(appSettings.AESEncryptPath) && !File.Exists(appSettings.AESEncryptPath))
            {
                result.AddError($"AESEncrypt executable '{appSettings.AESEncryptPath}' does not exist, please download from https://www.aescrypt.com/");
            }

            if (!string.IsNullOrEmpty(appSettings.LogDirectoryPath) && !Directory.Exists(appSettings.LogDirectoryPath))
            {
                result.AddError($"Log directory '{appSettings.LogDirectoryPath}' does not exist");
            }

            if (jobDetails.JobNameToRun is null)
            {
                result.AddError($"RunJobName is not supplied as parameter 1 or found in appsettings.json 'RunJobName'");
            }

            return result;
        }


    }
}
