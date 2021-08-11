using Archivist.Classes;
using Archivist.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace Archivist.Helpers
{
    internal static class ConfigurationHelpers
    {
        /// <summary>
        /// Just for initial development and generating test files, remove 
        /// passwords before adding to source control TODO
        /// </summary>
        /// <param name="configFileName"></param>
        internal static void CreateCustomConfiguration(string configFileName)
        {
            if (File.Exists(configFileName))
            {
                File.Delete(configFileName);
            }

            var backupTypeTest = new JobSpecification
            {
                Name = "TestBackup",
                WriteToConsole = true,
                PauseBeforeExit = true,
                ProcessTestOnly = true,
                ProcessSlowVolumes = false,
                ArchiveFairlyStatic = false,
                PrimaryArchiveDirectoryName = @"M:\Archive",
                EncryptionPassword = null,
                EncryptionPasswordFile = @"C:\Dev\Archivist\EncryptionPassword.txt"
            };

            var backupTypeQuick = new JobSpecification
            {
                Name = "QuickBackup",
                PauseBeforeExit = true,
                ProcessSlowVolumes = false,
                ArchiveFairlyStatic = false,
                PrimaryArchiveDirectoryName = @"M:\Archive",
                EncryptionPassword = null,
                EncryptionPasswordFile = @"C:\Dev\Archivist\EncryptionPassword.txt"
            };

            var backupTypeFull = new JobSpecification
            {
                Name = "FullBackup",
                ProcessTestOnly = false,
                PauseBeforeExit = true,
                ProcessSlowVolumes = true,
                ArchiveFairlyStatic = true,
                PrimaryArchiveDirectoryName = @"M:\Archive",
                EncryptionPassword = null,
                EncryptionPasswordFile = @"C:\Dev\Archivist\EncryptionPassword.txt"
            };

            var backupTypeScheduled = new JobSpecification
            {
                Name = "ScheduledBackup",
                WriteToConsole = false,
                PauseBeforeExit = false,
                ProcessSlowVolumes = true,
                ArchiveFairlyStatic = true,
                PrimaryArchiveDirectoryName = @"M:\Archive",
                EncryptionPassword = null,
                EncryptionPasswordFile = @"C:\Dev\Archivist\EncryptionPassword.txt"
            };

            var config = new Configuration
            {
                JobSpecifications = new() { backupTypeQuick, backupTypeFull, backupTypeScheduled, backupTypeTest },
                GlobalSecureDirectories = new List<SecureDirectory> {
                    new SecureDirectory
                    {
                        IsEnabled = true,
                        SynchoniseFileTimestamps = true,
                        DirectoryPath = @"C:\Personal\Secure"
                    }
                },
                GlobalSourceDirectories = new List<SourceDirectory> {
                    new SourceDirectory {
                        IsEnabled = true,
                        IsForTesting = false,
                        DirectoryPath = @"C:\Batch",
                        OutputFileName = null,
                        AddVersionSuffix = true,
                        RetainVersions = 2,
                        MinutesOldThreshold = 60
                    },
                    new SourceDirectory {
                        Priority = 3,
                        IsEnabled = true,
                        IsForTesting = false,
                        DirectoryPath = @"C:\Personal",
                        OutputFileName = null,
                        AddVersionSuffix = true,
                        RetainVersions = 5,
                        MinutesOldThreshold = 30
                    },
                    new SourceDirectory {
                        Priority = 4,
                        IsEnabled = true,
                        IsForTesting = false,
                        CheckTaskNameIsNotRunning = "Thunderbird",
                        DirectoryPath = @"C:\Users\Chris\AppData\Roaming\Thunderbird",
                        OutputFileName = null,
                        AddVersionSuffix = true,
                        RetainVersions = 2
                    },
                    new SourceDirectory {
                        IsEnabled = true,
                        IsForTesting = false,
                        DirectoryPath = @"C:\PowerShell",
                        AddVersionSuffix = true,
                        RetainVersions = 2,
                        MinutesOldThreshold = 30
                    },
                    new SourceDirectory {
                        Priority = 5,
                        IsEnabled = true,
                        IsForTesting = false,
                        IsFairlyStatic = true,
                        DirectoryPath = @"M:\Photos",
                        AddVersionSuffix = true,
                        EncryptOutput = true,
                        RetainVersions = 2,
                        MinutesOldThreshold = 60
                    },
                    new SourceDirectory {
                        Priority = 10,
                        IsEnabled = false,
                        IsForTesting = false,
                        DirectoryPath = @"D:\Incoming",
                        AddVersionSuffix = true,
                        RetainVersions = 2
                    },
                    new SourceDirectory {
                        Priority = 1,
                        IsEnabled = true,
                        IsForTesting = false,
                        DirectoryPath = @"C:\Dev",
                        AddVersionSuffix = true,
                        RetainVersions = 10,
                        CompressionLevel = CompressionLevel.Fastest,
                        MinutesOldThreshold = 30
                    },
                    new SourceDirectory {
                        Priority = 1,
                        IsEnabled = true,
                        IsForTesting = false,
                        DirectoryPath = @"C:\RamDiskImages",
                        AddVersionSuffix = true,
                        RetainVersions = 2,
                        MinutesOldThreshold = 60
                    },
                    new SourceDirectory {
                        IsEnabled = true,
                        IsFairlyStatic = true,
                        DirectoryPath = @"M:\Media\Audiobooks",
                        CompressionLevel = CompressionLevel.NoCompression,
                        AddVersionSuffix = false,
                        MinutesOldThreshold = 60
                    },
                    new SourceDirectory {
                        IsEnabled = true,
                        IsFairlyStatic = true,
                        DirectoryPath = @"M:\Media\Books",
                        CompressionLevel = CompressionLevel.Optimal,
                        AddVersionSuffix = false,
                        MinutesOldThreshold = 60
                    },
                    new SourceDirectory {
                        IsEnabled = true,
                        IsFairlyStatic = true,
                        DirectoryPath = @"M:\Media\Music",
                        CompressionLevel = CompressionLevel.NoCompression,
                        AddVersionSuffix = false,
                        MinutesOldThreshold = 60
                    },
                    new SourceDirectory {
                        IsEnabled = true,
                        IsFairlyStatic = true,
                        DirectoryPath = @"M:\Media\Radio",
                        CompressionLevel = CompressionLevel.NoCompression,
                        AddVersionSuffix = false,
                        MinutesOldThreshold = 60
                    },
                    new SourceDirectory {
                        IsEnabled = true,
                        IsFairlyStatic = true,
                        DirectoryPath = @"M:\Media\Video\BBC",
                        CompressionLevel = CompressionLevel.NoCompression,
                        AddVersionSuffix = false,
                        MinutesOldThreshold = 60
                    },
                    new SourceDirectory {
                        IsEnabled = true,
                        IsFairlyStatic = true,
                        DirectoryPath = @"M:\Media\Video\People",
                        CompressionLevel = CompressionLevel.NoCompression,
                        AddVersionSuffix = false,
                        MinutesOldThreshold = 60
                    },
                    new SourceDirectory {
                        IsEnabled = true,
                        IsFairlyStatic = true,
                        DirectoryPath = @"M:\Media\Video\Poker",
                        CompressionLevel = CompressionLevel.NoCompression,
                        AddVersionSuffix = false,
                        MinutesOldThreshold = 60
                    },
                    new SourceDirectory {
                        IsEnabled = true,
                        IsFairlyStatic = true,
                        DirectoryPath = @"M:\Media\Video\Subjects",
                        CompressionLevel = CompressionLevel.NoCompression,
                        AddVersionSuffix = false,
                        MinutesOldThreshold = 60
                    },
                    new SourceDirectory {
                        IsEnabled = true,
                        IsFairlyStatic = true,
                        DirectoryPath = @"M:\Media\TV",
                        CompressionLevel = CompressionLevel.NoCompression,
                        AddVersionSuffix = false,
                        MinutesOldThreshold = 60
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
                        AddVersionSuffix = false,
                        MinutesOldThreshold = 60
                    },
                    new SourceDirectory {
                        Priority = 3,
                        IsEnabled = true,
                        IsFairlyStatic = true,
                        DirectoryPath = @"D:\Creations",
                        CompressionLevel = CompressionLevel.Fastest,
                        AddVersionSuffix = true,
                        RetainVersions = 2,
                        MinutesOldThreshold = 60
                    },
                    new SourceDirectory {
                        Priority = 2,
                        IsEnabled = true,
                        DirectoryPath = @"C:\SQL\Backup",
                        CompressionLevel = CompressionLevel.NoCompression,
                        AddVersionSuffix = true,
                        RetainVersions = 2,
                        MinutesOldThreshold = 0
                    }
                },
                GlobalArchiveDirectories = new List<ArchiveDirectory>
                {
                    new ArchiveDirectory {
                        Priority = 3,
                        Description = "MicroSD card 500GB, connected on demand",
                        IsSlowVolume = true,
                        IsEnabled = true,
                        IsRemovable = true,
                        SynchoniseFileTimestamps = true,
                        IncludeSpecifications = new List<string> { "*.zip" },
                        ExcludeSpecifications = new List<string> { "Media-*.*", "Temp*.*", "Incoming*.*" },
                        DirectoryPath = @"S:\Archive",
                        RetainVersions = 5
                    },
                    new ArchiveDirectory {
                        Priority = 1,
                        Description = "External SSD set, 2 x 476GB, connected alternately on demand",
                        IsSlowVolume = true,
                        IsEnabled = true,
                        IsForTesting = true,
                        IsRemovable = true,
                        SynchoniseFileTimestamps = true,
                        IncludeSpecifications = new List<string> { "*.zip" },
                        ExcludeSpecifications = new List<string> { "Media-*.*", "Temp*.*", "Incoming*.*" },
                        DirectoryPath = @"Y:\Archive",
                        RetainVersions = 10
                    },
                    new ArchiveDirectory {
                        Priority = 2,
                        Description = "External WD 1TB drive, connected almost all the time",
                        IsSlowVolume = true,
                        IsEnabled = true,
                        IsForTesting = true,
                        IsRemovable = true,
                        SynchoniseFileTimestamps = true,
                        IncludeSpecifications = new List<string> { "*.zip" },
                        ExcludeSpecifications = new List<string> { },
                        DirectoryPath = @"Z:\Archive",
                        RetainVersions = 10
                    }
                }                
            };

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(configFileName, json);
        }

        internal static Configuration LoadConfiguration(string configFileName)
        {
            if (File.Exists(configFileName))
            {
                string json = File.ReadAllText(configFileName);

                var config = JsonSerializer.Deserialize<Configuration>(json);

                config.LoadedFromFile = configFileName;

                return config;
            }
            else
            {
                throw new Exception($"Config file {configFileName} does not exist");
            }
        }

        /// <summary>
        /// Creates a default, and invalid by design configuration file that must be 
        /// customised. This will be created on first run (or if the file does not exist), the
        /// errors will be reported and the program will terminate without doing anything.
        /// </summary>
        /// <param name="configFileName"></param>
        internal static void CreateDefaultConfiguration(string configFileName, bool alwaysOverwrite = true)
        {
            if (File.Exists(configFileName) && alwaysOverwrite)
            {
                File.Delete(configFileName);
            }

            var backupTypeTest = new JobSpecification
            {
                Name = "TestBackup",
                WriteToConsole = true,
                PauseBeforeExit = true,
                ProcessTestOnly = true,
                ProcessSlowVolumes = false,
                ArchiveFairlyStatic = false,
                PrimaryArchiveDirectoryName = @"M:\PrimaryArchiveFolderName",
                EncryptionPassword = "passwordinplaintextscary",
            };

            var backupTypeQuick = new JobSpecification
            {
                Name = "QuickBackup",
                PauseBeforeExit = true,
                ProcessSlowVolumes = false,
                ArchiveFairlyStatic = false,
                PrimaryArchiveDirectoryName = @"M:\PrimaryArchiveFolderName",
                EncryptionPasswordFile = @"C:\\DirectoryName\\PasswordInTextFile.txt",
            };

            var config = new Configuration
            {
                JobSpecifications = new() { backupTypeQuick, backupTypeTest },
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
                        DirectoryPath = @"S:\ArchivedFilesBlah",
                        RetainVersions = 2
                    },
                    new ArchiveDirectory {
                        Priority = 1,
                        Description = "External SSD set, 2 x 476GB, connected alternately on demand",
                        IsSlowVolume = true,
                        IsEnabled = true,
                        IsForTesting = true,
                        IsRemovable = true,
                        SynchoniseFileTimestamps = true,
                        IncludeSpecifications = new List<string> { "*.zip" },
                        ExcludeSpecifications = new List<string> { "Media-*.*", "Temp*.*", "Incoming*.*" },
                        DirectoryPath = @"Y:\ArchivedFilesAgain",
                        RetainVersions = 2
                    },
                    new ArchiveDirectory {
                        Priority = 2,
                        Description = "External massive HDD, connected almost all the time",
                        IsSlowVolume = true,
                        IsEnabled = true,
                        IsForTesting = true,
                        IsRemovable = true,
                        SynchoniseFileTimestamps = true,
                        IncludeSpecifications = new List<string> { "*.zip" },
                        ExcludeSpecifications = new List<string> { },
                        DirectoryPath = @"Z:\Archive",
                        RetainVersions = 4
                    }
                }
            };

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(configFileName, json);
        }

        /// <summary>
        /// Go through the job specification and validate it, only directories that will be processed 
        /// for this job will be validated
        /// </summary>
        /// <returns></returns>
        internal static Result ValidateJobSpecification(JobSpecification jobSpec)
        {
            Result result = new("ValidateJobSpecification", false);

            if (string.IsNullOrEmpty(jobSpec.PrimaryArchiveDirectoryName))
            {
                result.AddError("PrimaryArchiveDirectoryName is empty");
            }

            if (!Directory.Exists(jobSpec.PrimaryArchiveDirectoryName))
            {
                result.AddError($"PrimaryArchiveDirectoryName '{jobSpec.PrimaryArchiveDirectoryName}' does not exist");
            }

            foreach (var src in jobSpec.SourceDirectories.Where(_ => _.IsToBeProcessed(jobSpec)))
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
            }

            foreach (var arc in jobSpec.ArchiveDirectories.Where(_ => _.IsToBeProcessed(jobSpec)))
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
            }

            foreach (var arc in jobSpec.ArchiveDirectories
                .Where(_ => _.IsToBeProcessed(jobSpec)))
            {
                foreach (var src in jobSpec.SourceDirectories
                    .Where(_ => _.IsToBeProcessed(jobSpec)))
                {
                    if (arc.RetainVersions > 0 && arc.RetainVersions < src.RetainVersions)
                    {
                        result.AddWarning($"'{src.DirectoryPath}' has RetainVersions {src.RetainVersions} for source '{src.DirectoryPath}' and {arc.RetainVersions} for archive to '{arc.DirectoryPath}', this will mean earlier generations of the archive get copied over from source to archive and immediately deleted, best to always retain the same or more versions in the archives than the sources");
                    }
                }
            }

            return result;
        }
    }
}
