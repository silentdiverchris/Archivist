﻿using Archivist.Classes;
using Archivist.Models;
using System.IO.Compression;
using System.Text.Json;

namespace Archivist.Utilities
{
    internal static class AppSettingsUtilities
    {
        /// <summary>
        /// Creates a default, and invalid by design appsettings file that must be 
        /// customised. This will be created on first run (or if the file does not exist), the
        /// errors will be reported and the program will terminate without doing anything.
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
                        ProcessTestOnly = true,
                        ProcessSlowVolumes = false,
                        ArchiveFairlyStatic = false,
                        PrimaryArchiveDirectoryName = @"M:\PrimaryArchiveDirectoryName"
                    },
                    new Job
                    {
                        Name = "ExampleJob2",
                        Description = "Another example of a job specification",
                        PauseBeforeExit = true,
                        ProcessSlowVolumes = false,
                        ArchiveFairlyStatic = false,
                        PrimaryArchiveDirectoryName = @"M:\PrimaryArchiveDirectoryName",
                        EncryptionPasswordFile = @"C:\InvalidDirectoryName\PasswordInTextFile.txt",
                    }
                },
                GlobalSecureDirectories = new List<SecureDirectory> {
                    new SecureDirectory
                    {
                        IsEnabled = true,
                        SynchoniseFileTimestamps = true,
                        DirectoryPath = @"C:\Something\Secure"
                    },
                    new SecureDirectory
                    {
                        IsEnabled = true,
                        SynchoniseFileTimestamps = true,
                        DirectoryPath = @"C:\SomethingElse\AlsoSecure"
                    },
                },
                GlobalSourceDirectories = new List<SourceDirectory> {
                    new SourceDirectory {
                        IsEnabled = true,
                        IsForTesting = false,
                        DirectoryPath = @"C:\ProbablyDoesntExist",
                        OutputFileName = null,
                        AddVersionSuffix = true,
                        RetainVersions = 2,
                        EncryptOutput = false,
                        DeleteArchiveAfterEncryption = true
                    },
                    new SourceDirectory {
                        Priority = 3,
                        IsEnabled = true,
                        IsForTesting = false,
                        DirectoryPath = @"C:\ProbablyDoesntExistEither",
                        OutputFileName = null,
                        AddVersionSuffix = true,
                        RetainVersions = 2,
                        EncryptOutput = true,
                        DeleteArchiveAfterEncryption = true
                    },
                    new SourceDirectory {
                        IsEnabled = true,
                        IsForTesting = true,
                        IsFairlyStatic = false,
                        DirectoryPath = @"D:\Temp",
                        CompressionLevel = CompressionLevel.Fastest,
                        AddVersionSuffix = true,
                        RetainVersions = 2,
                        MinutesOldThreshold = 60
                    },
                    new SourceDirectory {
                        IsEnabled = true,
                        IsFairlyStatic = true,
                        DirectoryPath = @"M:\Media\Movies",
                        CompressionLevel = CompressionLevel.NoCompression,
                        AddVersionSuffix = false
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
                        SynchoniseFileTimestamps = true,
                        IncludeSpecifications = new List<string> { "*.zip" },
                        ExcludeSpecifications = new List<string> { "Media-*.*", "Temp*.*", "Incoming*.*" },
                        VolumeLabel = "BigMicroSD-01",
                        DirectoryPath = "ArchivedFiles",
                        RetainVersions = 2,
                        RetainDaysOld = 90
                    },
                    new ArchiveDirectory {
                        Priority = 1,
                        Description = "External SSD connected on demand",
                        IsSlowVolume = true,
                        IsEnabled = true,
                        IsForTesting = true,
                        IsRemovable = true,
                        SynchoniseFileTimestamps = true,
                        IncludeSpecifications = new List<string> { "*.zip" },
                        ExcludeSpecifications = new List<string> { "Media-*.*", "Temp*.*", "Incoming*.*" },
                        VolumeLabel = "ExternalSSD-01",
                        DirectoryPath = "ArchivedFileDirectoryName",
                        RetainVersions = 5,
                        RetainDaysOld = 90
                    },
                    new ArchiveDirectory {
                        Priority = 1,
                        Description = "External HDD connected on demand just for latest versions of all media",
                        IsSlowVolume = true,
                        IsEnabled = true,
                        IsForTesting = true,
                        IsRemovable = true,
                        SynchoniseFileTimestamps = true,
                        IncludeSpecifications = new List<string> { "Media*.*" },
                        ExcludeSpecifications = new List<string> { },
                        VolumeLabel = "ExternalHDD-01",
                        DirectoryPath = "ArchivedFileDirectoryName",
                        RetainVersions = 1
                    },
                    new ArchiveDirectory {
                        Priority = 2,
                        Description = "Internal massive HDD",
                        IsSlowVolume = true,
                        IsEnabled = true,
                        IsForTesting = true,
                        IsRemovable = true,
                        SynchoniseFileTimestamps = true,
                        IncludeSpecifications = new List<string> { "*.zip" },
                        ExcludeSpecifications = new List<string> { },
                        DirectoryPath = @"Z:\Archive",
                        RetainVersions = 10,
                        RetainDaysOld = 365
                    }
                }
            };

            string json = JsonSerializer.Serialize(
                appSettings,
                options: new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(fileName, json);
        }

        internal static Result SelectAndValidateJob(JobDetails jobDetails)
        {
            Result result = new("ValidateAppSettings", false);

            if (!string.IsNullOrEmpty(jobDetails.AppSettings.AESEncryptPath) && !File.Exists(jobDetails.AppSettings.AESEncryptPath))
            {
                result.AddError($"AESCrypt executable '{jobDetails.AppSettings.AESEncryptPath}' does not exist");
            }

            string jobsNamesDefined = jobDetails.AppSettings.Jobs.Any()
                ? string.Join(',', jobDetails.AppSettings.Jobs.Select(_ => _.Name))
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

                result.SubsumeResult(jobDetails.AppSettings.SelectJob(jobDetails.JobNameToRun));

                if (jobDetails.SelectedJob is null)
                {
                    result.AddError($"Job '{jobDetails.JobNameToRun}' cannot be selected, available jobs are '{jobsNamesDefined}'");
                }
                else
                {
                    // We have successfully selected the job, validate it

                    result.AddInfo($"Selected job is '{jobDetails.SelectedJob.Name}'");

                    result.SubsumeResult(ValidateJob(jobDetails.SelectedJob));
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

            if (string.IsNullOrEmpty(job.PrimaryArchiveDirectoryName))
            {
                result.AddError("PrimaryArchiveDirectoryName is empty");
            }

            if (!Directory.Exists(job.PrimaryArchiveDirectoryName))
            {
                result.AddError($"PrimaryArchiveDirectoryName '{job.PrimaryArchiveDirectoryName}' does not exist");
            }

            foreach (var src in job.SourceDirectories.Where(_ => _.IsToBeProcessed(job)))
            {
                if (string.IsNullOrEmpty(src.DirectoryPath))
                {
                    result.AddError("SourceDirectories.DirectoryPath is empty");
                }

                if (!Directory.Exists(src.DirectoryPath) && !src.IsRemovable)
                {
                    result.AddError($"SourceDirectories.DirectoryPath '{src.DirectoryPath}' does not exist");
                }

                if (src.RetainVersions > 1 && !src.AddVersionSuffix)
                {
                    result.AddWarning($"SourceDirectories.RetainVersions = {src.RetainVersions} is pointless with AddversionSuffix false for source '{src.DirectoryPath}'");
                }

                if (src.RetainVersions < 0)
                {
                    result.AddError($"SourceDirectories.RetainVersions = {src.RetainVersions} is invalid for source '{src.DirectoryPath}'");
                }

                if (src.RetainDaysOld > 0 && src.RetainDaysOld < Constants.RETAIN_DAYS_OLD_MINIMUM)
                {
                    result.AddError($"ArchiveDirectories.RetainDaysOld = {src.RetainDaysOld} is invalid for archive '{src.DirectoryPath}', minimum is {Constants.RETAIN_DAYS_OLD_MINIMUM}");
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

                if (arc.RetainVersions < 0)
                {
                    result.AddError($"ArchiveDirectories.RetainVersions = {arc.RetainVersions} is invalid for archive '{arc.DirectoryPath}'");
                }

                if (arc.RetainDaysOld > 0 && arc.RetainDaysOld < Constants.RETAIN_DAYS_OLD_MINIMUM)
                {
                    result.AddError($"ArchiveDirectories.RetainDaysOld = {arc.RetainDaysOld} is invalid for archive '{arc.DirectoryPath}', minimum is {Constants.RETAIN_DAYS_OLD_MINIMUM}");
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

        internal static AppSettings LoadAppSettings(string fileName)
        {
            if (File.Exists(fileName))
            {
                string json = File.ReadAllText(fileName);

                var appSettings = JsonSerializer.Deserialize<AppSettings>(json);

                appSettings.LoadedFromFile = fileName;

                return appSettings;
            }
            else
            {
                throw new Exception($"App settings file {fileName} does not exist");
            }
        }
    }
}