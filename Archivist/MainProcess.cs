using Archivist.Classes;
using Archivist.Helpers;
using Archivist.Models;
using Archivist.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using static Archivist.Delegates;
using static Archivist.Enumerations;

namespace Archivist
{
    internal class MainProcess : IDisposable
    {
        private readonly JobSpecification _jobSpecification;
        private readonly JobDetails _jobDetails;
        private readonly LogService _logService;

        internal MainProcess(JobDetails jobDetails)
        {
            ConsoleDelegate consoleDelegate = new(WriteToConsole);

            try
            {
                _jobDetails = jobDetails;
#if DEBUG
                // For development, overwrite the config file with the declaration in the helper
                ConfigurationHelpers.CreateCustomConfiguration(jobDetails.ConfigFilePath);
#else
            if (!File.Exists(jobDetails.ConfigFilePath))
            {
                ConfigurationHelpers.CreateDefaultConfiguration(jobDetails.ConfigFilePath);

                // Create the custom config as well, just for now - reemove TODO
                ConfigurationHelpers.CreateCustomConfiguration(jobDetails.ConfigFilePath + ".json");
            }
#endif
                Configuration config = ConfigurationHelpers.LoadConfiguration(jobDetails.ConfigFilePath);

                Result selectJobResult = config.SelectJob(jobDetails.JobName);

                if (selectJobResult.HasNoErrors)
                {
                    _jobSpecification = config.SelectedJobSpecification;
                }
                else
                {
                    throw new Exception($"Failed to select job '{jobDetails.JobName}', {selectJobResult.TextSummary}");
                }
            }
            catch (Exception ex)
            {
                EventLogHelper.WriteEntry($"Exception in MainProcess constructor {ex.Message} {ex.Source}", enSeverity.Error);
                throw;
            }

            _logService = new LogService(jobDetails, consoleDelegate);
        }

        internal async Task RunAsync()
        {
            Result result = new("Archivist", true, $"job '{_jobDetails.JobName}'");
            result.AddInfo($"Config file '{_jobDetails.ConfigFilePath}'");
            result.AddInfo($"Log file '{_logService.LogFileName}'");

            result.SubsumeResult(ValidateJobDetails(_jobDetails));
            result.SubsumeResult(ConfigurationHelpers.ValidateJobSpecification(_jobSpecification));

            await _logService.ProcessResult(result);

            if (result.HasNoErrors)
            {
                if (_jobDetails.AesEncryptExecutable is not null)
                {
                    using (var secureDirectoryService = new SecureDirectoryService(_jobSpecification, _logService, _jobDetails.AesEncryptExecutable))
                    {
                        Result secureDirectoryResult = await secureDirectoryService.ProcessSecureDirectoriesAsync();
                        result.SubsumeResult(secureDirectoryResult);
                    }
                }

                if (!result.HasErrors)
                {
                    using (var compressionService = new CompressionService(_jobSpecification, _logService, _jobDetails.AesEncryptExecutable))
                    {
                        Result compressionResult = await compressionService.CompressSources();
                        result.SubsumeResult(compressionResult);
                    }

                    if (!result.HasErrors)
                    {
                        using (var fileService = new FileService(_jobSpecification, _logService))
                        {
                            Result copyArchivesResult = await fileService.CopyToArchives();
                            result.SubsumeResult(copyArchivesResult);
                        }
                    }
                }
            }
            else
            {
                result.AddError("One or more errors or warnings found in configuraiton, not running job");
            }

            _jobDetails.EndedUtc = DateTime.UtcNow;

            if (result.HasErrors)
            {
                EventLogHelper.WriteEntry($"Archivist completed job {_jobDetails.JobName} with errors", enSeverity.Error);
                result.AddError($"Job {_jobDetails.JobName} completed with errors");
            }
            else if (result.HasWarnings)
            {
                EventLogHelper.WriteEntry($"Archivist completed job {_jobDetails.JobName} with warnings", enSeverity.Warning);
                result.AddWarning($"Job {_jobDetails.JobName} completed with warnings");
            }
            else
            {
                EventLogHelper.WriteEntry($"Archivist completed job {_jobDetails.JobName} successfully", enSeverity.Info);
                result.AddSuccess($"Job {_jobDetails.JobName} completed successfully");
            }

            await _logService.ProcessResult(result);

            if (_jobSpecification.PauseBeforeExit)
            {
                Console.WriteLine("Press any key to exit");
                Console.ReadLine();
            }
        }

        internal void WriteToConsole(LogEntry entry)
        {
            if (_jobSpecification.WriteToConsole)
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

        public void Dispose()
        {
            if (_logService is not null)
                _logService.Dispose();
        }

        private static Result ValidateJobDetails(JobDetails jobDetails)
        {
            Result result = new("ValidateJobDetails", false);

            if (jobDetails.ConfigFilePath is null)
            {
                result.AddError($"Config file name is not supplied in AppSettings.json 'ConfigurationFile'");
            }
            else if (!File.Exists(jobDetails.ConfigFilePath))
            {
                result.AddError($"Configuration file '{jobDetails.ConfigFilePath}' does not exist");
            }

            if (!string.IsNullOrEmpty(jobDetails.AesEncryptExecutable) && !File.Exists(jobDetails.AesEncryptExecutable))
            {
                result.AddError($"AESEncrypt executable '{jobDetails.AesEncryptExecutable}' does not exist, please download from https://www.aescrypt.com/");
            }

            if (jobDetails.LogDirectoryName is not null && !Directory.Exists(jobDetails.LogDirectoryName))
            {
                result.AddError($"Log directory '{jobDetails.LogDirectoryName}' does not exist");
            }

            if (jobDetails.JobName is null)
            {
                result.AddError($"RunJobName is not supplied as parameter 1 or found in appsettings.json 'RunJobName'");
            }

            return result;
        }


    }
}
