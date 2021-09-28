using Archivist.Classes;
using Archivist.Helpers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace Archivist.Models
{
    /// <summary>
    /// The structure that the appsettings.json is loaded into on startup and then referenced 
    /// to each service, plus functionality for selecting which job is to be run.
    /// </summary>
    public class AppSettings
    {
        public bool UseUtcTime { get; set; }
        public string? DefaultJobName { get; set; }
        public string? LogDirectoryPath { get; set; }
        public string? AESEncryptPath { get; set; }
        public string? SqlConnectionString { get; set; }
        
        /// <summary>
        /// Send more details progress information to the console
        /// </summary>
        public bool VerboseConsole { get; set; }

        /// <summary>
        /// Send progress information to the event log
        /// </summary>
        public bool VerboseEventLog { get; set; }

        /// <summary>
        /// The various different jobs, each has it's own individual
        /// configuration, eg. for a daily backup, weekly, monthly etc.
        /// </summary>
        public List<Job> Jobs { get; set; } = new();

        /// <summary>
        /// These directories are added to the directory lists for any job that is run
        /// </summary>
        public List<SourceDirectory> GlobalSourceDirectories { get; set; } = new();
        public List<ArchiveDirectory> GlobalArchiveDirectories { get; set; } = new();
        public List<SecureDirectory> GlobalSecureDirectories { get; set; } = new();

        [JsonIgnore]
        public string? LoadedFromFile { get; set; }

        [JsonIgnore]
        internal Job? SelectedJob { get; private set; } = null;

        internal Result SelectJob(string jobName)
        {
            Result result = new("SelectJob");

            // If it fails, don't leave any previously selected job in here
            SelectedJob = null;

            if (jobName.Contains(' '))
            {
                result.AddError("Job name cannot contain spaces");
            }
            else
            {
                SelectedJob = Jobs.SingleOrDefault(_ => _.Name == jobName);

                if (SelectedJob is not null)
                {
                    SelectedJob.SourceDirectories.AddRange(GlobalSourceDirectories);
                    SelectedJob.ArchiveDirectories.AddRange(GlobalArchiveDirectories);
                    SelectedJob.SecureDirectories.AddRange(GlobalSecureDirectories);

                    // VerifyVolume now gets called first time the DirectoryPath property is used
                    //foreach (var dir in SelectedJob.SourceDirectories)
                    //{
                    //    dir.VerifyVolume();
                    //}

                    //foreach (var dir in SelectedJob.ArchiveDirectories)
                    //{
                    //    dir.VerifyVolume();
                    //}

                    //foreach (var dir in SelectedJob.SecureDirectories)
                    //{
                    //    dir.VerifyVolume();
                    //}

                    if (!string.IsNullOrEmpty(SelectedJob.EncryptionPasswordFile))
                    {
                        if (File.Exists(SelectedJob.EncryptionPasswordFile))
                        {
                            if (!string.IsNullOrEmpty(SelectedJob.EncryptionPassword))
                            {
                                result.AddWarning($"EncryptionPasswordFile will override EncryptionPassword value for job {SelectedJob.Name}");
                            }

                            SelectedJob.EncryptionPassword = File.ReadAllText(SelectedJob.EncryptionPasswordFile);
                        }
                        else
                        {
                            result.AddError($"EncryptionPasswordFile '{SelectedJob.EncryptionPasswordFile}' does not exist");
                        }
                    }
                }
                else
                {
                    if (Jobs.Any())
                    {
                        string jobNameList = Jobs is not null
                            ? Jobs.Select(_ => _.Name)!.ConcatenateToDelimitedList()
                            : "[no jobs defined]";

                        result.AddError($"cannot find job specification '{jobName}', available names are {jobNameList}, loaded from '{LoadedFromFile}'");
                    }
                    else
                    {
                        result.AddError($"cannot find job specification '{jobName}', no job names loaded from '{LoadedFromFile}'");
                    }
                }
            }

            return result;
        }
    }
}