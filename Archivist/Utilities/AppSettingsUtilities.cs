using Archivist.Classes;
using Archivist.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace Archivist.Utilities
{
    internal static class AppSettingsUtilities
    {
        /// <summary>
        /// Creates a default, and invalid-by-design appsettings file that must be 
        /// customised. This will be created on first run (or if the file does not exist), the
        /// errors will be reported, the program will terminate without doing anything and the 
        /// settings can be adjusted.
        /// </summary>
        /// <param name="fileName"></param>
        internal static void CreateDefaultAppSettings(string fileName)
        {
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            var appSettings = new AppSettings
            {
                DefaultJobName = "ExampleJob1",
                AESEncryptPath = "",
                LogDirectoryPath = "Log",
                SqlConnectionString = "",
                VerboseConsole = false,
                VerboseEventLog = false,
                Jobs = new() 
                {
                    new Job
                    {
                        Name = "ExampleJob1",
                        Description = "An example of a job specification, you will need to edit this to point it at a primary archive directory, and any other changes you want to make.",
                        WriteToConsole = true,
                        PauseBeforeExit = true,
                        ProcessSlowVolumes = false,
                        PrimaryArchiveDirectoryPath = @"M:\PrimaryArchiveDirectoryName"
                    },
                    new Job
                    {
                        Name = "ExampleJob2",
                        Description = "Another example of a job specification",
                        PauseBeforeExit = true,
                        ProcessSlowVolumes = false,
                        PrimaryArchiveDirectoryPath = @"M:\PrimaryArchiveDirectoryName",
                        EncryptionPasswordFile = @"C:\InvalidDirectoryName\PasswordInTextFile.txt",
                    }
                },
                GlobalSecureDirectories = new List<SecureDirectory> {
                    new SecureDirectory
                    {
                        IsEnabled = true,
                        DirectoryPath = @"C:\Something\Secure"
                    },
                    new SecureDirectory
                    {
                        IsEnabled = true,
                        DirectoryPath = @"C:\SomethingElse\AlsoSecure"
                    },
                },
                GlobalSourceDirectories = new List<SourceDirectory> {
                    new SourceDirectory {
                        IsEnabled = true,
                        DirectoryPath = @"C:\ProbablyDoesntExist",
                        RetainMaximumVersions = 3,
                        EncryptOutput = false
                    },
                    new SourceDirectory {
                        Priority = 3,
                        IsEnabled = true,
                        DirectoryPath = @"C:\ProbablyDoesntExistEither",
                        RetainMaximumVersions = 5,
                        EncryptOutput = true
                    },
                    new SourceDirectory {
                        IsEnabled = true,
                        DirectoryPath = @"D:\Temp",
                        CompressionLevel = CompressionLevel.Fastest,
                        RetainMaximumVersions = 2,
                        MinutesOldThreshold = 60
                    },
                    new SourceDirectory {
                        IsEnabled = true,
                        DirectoryPath = @"M:\Media\Movies",
                        CompressionLevel = CompressionLevel.NoCompression
                    }
                },
                GlobalArchiveDirectories = new List<ArchiveDirectory>
                {
                    new ArchiveDirectory {
                        Priority = 3,
                        Description = "A very slow but big and cheap MicroSD card",
                        IsSlowVolume = true,
                        IsEnabled = true,
                        IsRemovable = true,
                        IncludeSpecifications = new List<string> { "*.zip" },
                        ExcludeSpecifications = new List<string> { "Media-*.*", "Temp*.*", "Incoming*.*" },
                        VolumeLabel = "BigMicroSD-01",
                        DirectoryPath = "ArchivedFiles",
                        RetainMaximumVersions = 2,
                        RetainYoungerThanDays = 90
                    },
                    new ArchiveDirectory {
                        Priority = 1,
                        Description = "External SSD connected on demand",
                        IsSlowVolume = true,
                        IsEnabled = true,
                        IsRemovable = true,
                        IncludeSpecifications = new List<string> { "*.zip" },
                        ExcludeSpecifications = new List<string> { "Media-*.*", "Temp*.*", "Incoming*.*" },
                        VolumeLabel = "ExternalSSD-01",
                        DirectoryPath = "ArchivedFileDirectoryName",
                        RetainMaximumVersions = 5,
                        RetainYoungerThanDays = 90
                    },
                    new ArchiveDirectory {
                        Priority = 1,
                        Description = "External HDD connected on demand just for latest versions of all media",
                        IsSlowVolume = true,
                        IsEnabled = true,
                        IsRemovable = true,
                        IncludeSpecifications = new List<string> { "Media*.*" },
                        ExcludeSpecifications = new List<string> { },
                        VolumeLabel = "ExternalHDD-01",
                        DirectoryPath = "ArchivedFileDirectoryName",
                        RetainMaximumVersions = 10,
                    },
                    new ArchiveDirectory {
                        Priority = 2,
                        Description = "Internal massive HDD",
                        IsSlowVolume = true,
                        IsEnabled = true,
                        IsRemovable = true,
                        IncludeSpecifications = new List<string> { "*.zip" },
                        ExcludeSpecifications = new List<string> { },
                        DirectoryPath = @"Z:\Archive",
                        RetainMaximumVersions = 10,
                        RetainYoungerThanDays = 365
                    }
                }
            };

            string json = JsonSerializer.Serialize(
                appSettings,
                options: new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(fileName, json);
        }

        internal static Result SelectAndValidateJob(JobDetails jobDetails, AppSettings appSettings)
        {
            Result result = new("SelectAndValidateJob", false);

            if (!string.IsNullOrEmpty(appSettings.AESEncryptPath) && !File.Exists(appSettings.AESEncryptPath))
            {
                result.AddError($"AESCrypt executable '{appSettings.AESEncryptPath}' does not exist");
            }

            string? jobsNamesDefined = appSettings.Jobs is not null && appSettings.Jobs.Any()
                ? string.Join(',', appSettings.Jobs.Select(_ => _.Name))
                : null;

            if (jobsNamesDefined is null)
            {
                result.AddError("No jobs are defined");
            }
            else if (string.IsNullOrEmpty(jobDetails.JobNameToRun))
            {
                result.AddError($"No job has been specified via parameter or DefaultJobName, available jobs are '{jobsNamesDefined}'");
            }
            else
            {
                // OK, let's try to select the specified job

                result.SubsumeResult(appSettings.SelectJob(jobDetails.JobNameToRun));

                if (appSettings.SelectedJob is null)
                {
                    result.AddError($"Job '{jobDetails.JobNameToRun}' cannot be selected, available jobs are '{jobsNamesDefined}'");
                }
                else
                {
                    // We have successfully selected the job, validate it

                    result.AddInfo($"Selected job is '{appSettings.SelectedJob.Name}'");

                    result.SubsumeResult(ValidateJob(appSettings.SelectedJob));
                }
            }

            return result;
        }

        /// <summary>
        /// Go through the job specification and validate it, only directories that will be processed 
        /// for this job will be validated
        /// </summary>
        /// <returns></returns>
        private static Result ValidateJob(Job job)
        {
            Result result = new("ValidateJobSpecification", false);

            if (string.IsNullOrEmpty(job.PrimaryArchiveDirectoryPath))
            {
                result.AddError("PrimaryArchiveDirectoryName is empty");
            }

            if (!Directory.Exists(job.PrimaryArchiveDirectoryPath))
            {
                result.AddError($"PrimaryArchiveDirectoryName '{job.PrimaryArchiveDirectoryPath}' does not exist");
            }

            foreach (var dup in job.SourceDirectories.Where(_ => _.IsEnabled).GroupBy(_ => _.VolumeLabel + " " + _.DirectoryPath).Where(g => g.Count() > 1).Select(y => y).ToList())
            {
                result.AddError($"Duplicate enabled source directory '{dup.Key}'");
            }

            foreach (var src in job.SourceDirectories.Where(_ => _.IsToBeProcessed(job)))
            {
                if (src.DirectoryPath.IsEmpty())
                {
                    result.AddError("SourceDirectories.DirectoryPath is empty");
                }

                if (!Directory.Exists(src.DirectoryPath) && !src.IsRemovable)
                {
                    result.AddError($"SourceDirectories.DirectoryPath '{src.DirectoryPath}' does not exist");
                }

                if (src.RetainMaximumVersions < Constants.RETAIN_VERSIONS_MINIMUM)
                {
                    result.AddError($"ArchiveDirectories.RetainMaximumVersions = {src.RetainMaximumVersions} is invalid for archive '{src.DirectoryPath}'");
                }

                if (src.RetainYoungerThanDays < Constants.RETAIN_DAYS_OLD_MINIMUM)
                {
                    result.AddError($"ArchiveDirectories.RetainYoungerThanDays = {src.RetainYoungerThanDays} is invalid for archive '{src.DirectoryPath}'");
                }
            }

            foreach (var arc in job.ArchiveDirectories.Where(_ => _.IsToBeProcessed(job)))
            {
                if (string.IsNullOrEmpty(arc.DirectoryPath))
                {
                    result.AddError("ArchiveDirectories.DirectoryPath is empty");
                }

                if (!Directory.Exists(arc.DirectoryPath) && !arc.IsRemovable)
                {
                    result.AddError($"ArchiveDirectories.DirectoryPath '{arc.DirectoryPath}' does not exist");
                }

                if (arc.RetainMaximumVersions < Constants.RETAIN_VERSIONS_MINIMUM)
                {
                    result.AddError($"ArchiveDirectories.RetainMaximumVersions = {arc.RetainMaximumVersions} is invalid for archive '{arc.DirectoryPath}'");
                }

                if (arc.RetainYoungerThanDays < 0)
                {
                    result.AddError($"ArchiveDirectories.RetainYoungerThanDays = {arc.RetainYoungerThanDays} is invalid for archive '{arc.DirectoryPath}'");
                }
            }

            foreach (var sec in job.SecureDirectories.Where(_ => _.IsToBeProcessed(job)))
            {
                if (string.IsNullOrEmpty(sec.DirectoryPath))
                {
                    result.AddError("SecureDirectories.DirectoryPath is empty");
                }

                if (!Directory.Exists(sec.DirectoryPath) && !sec.IsRemovable)
                {
                    result.AddError($"SecureDirectories.DirectoryPath '{sec.DirectoryPath}' does not exist");
                }
            }

            return result;
        }

        internal static AppSettings? LoadAppSettings(string fileName)
        {
            if (File.Exists(fileName))
            {
                string json = File.ReadAllText(fileName);

                var options = new JsonSerializerOptions
                {
                    // err, nothing right now thanks
                };

                var appSettings = JsonSerializer.Deserialize<AppSettings>(json, options);

                if (appSettings is not null)
                {
                    appSettings.LoadedFromFile = fileName;
                }

                return appSettings;
            }
            else
            {
                throw new Exception($"App settings file {fileName} does not exist");
            }
        }
    }
}
