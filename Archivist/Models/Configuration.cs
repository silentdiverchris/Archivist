using Archivist.Classes;
using Microsoft.SqlServer.Management.Smo.Agent;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Serialization;

namespace Archivist.Models
{
    public class BaseDirectory
    {
        /// <summary>
        /// Process directories in this order, lowest number first (then by directory name alpha)
        /// </summary>
        public int Priority { get; set; } = 99;

        /// <summary>
        /// Only process this directory after this hour starts, zero disables
        /// </summary>
        public int EnabledAtHour { get; set; } = 0;

        /// <summary>
        /// Only process this directory before this hour starts, zero disables
        /// </summary>
        public int DisabledAtHour { get; set; } = 0;

        /// <summary>
        /// Marks this directory as one to process when the backup type has ProcessTestOnly set, this
        /// is just used for developing the code
        /// </summary>
        public bool IsForTesting { get; set; }

        /// <summary>
        /// A human description of what this directory is, or what drive it's 
        /// on - eg '128gb USB Key' or 'Where my photos are', this doesn't make anything work or
        /// break anything if left undefined, it's just a reminder for the human.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Whether to process this directory in any way
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Whether this is on a volume that is removeable, mainly determining whether it should 
        /// be considered an error if the volume it's on can't be found. If it can't be found it
        /// won't even be considered as a warning if this is true.
        /// </summary>
        public bool IsRemovable { get; set; }

        /// <summary>
        /// Whether this is a slow volume, used in conjunction with config WriteToSlowVolumes so
        /// backup jobs that only read from and write to fast drives can be set up by setting job 
        /// setting ProcessSlowVolumes to false.
        /// </summary>
        public bool IsSlowVolume { get; set; }

        /// <summary>
        /// If a file has a version suffix (created by setting source directory setting AddVersionSuffix 
        /// to true) we will retain this many of them in this directory, zero means we keep all versions, which
        /// will eventually fill the volume. One gotcha is that if you set this lower on an archive directory
        /// than on the source directory the system will keep copying over older versions and then deleting 
        /// them, watch out for that.
        /// </summary>
        public int RetainVersions { get; set; } = 1;

        /// <summary>
        /// The full path of this directory
        /// </summary>
        public string DirectoryPath { get; set; }

        /// <summary>
        /// A list of file specs, eg "*.txt', 'thing.*', abc???de.jpg' etc, only process files matching these, an
        /// empty list includes all files
        /// </summary>
        public List<string> IncludeSpecifications { get; set; }

        /// <summary>
        /// A list of file specs, eg "*.txt', 'thing.*', abc???de.jpg' etc, ignore files matching these, an
        /// empty list doesn't exclude any files
        /// </summary>
        public List<string> ExcludeSpecifications { get; set; }

        /// <summary>
        /// Set the creation and last write time of any files created to the same as the source
        /// </summary>
        public bool SynchoniseFileTimestamps { get; set; } = true;

        public bool IsToBeProcessed(JobSpecification jobSpec)
        {
            if (!IsEnabled)
                return false;

            if (IsSlowVolume && jobSpec.ProcessSlowVolumes == false)
                return false;

            if (jobSpec.ProcessTestOnly && IsForTesting == false)
                return false;

            if (EnabledAtHour != 0 || DisabledAtHour != 0)
            {
                var currentHour = DateTime.Now.Hour;

                if (EnabledAtHour != 0 && currentHour < EnabledAtHour)
                    return false;

                if (DisabledAtHour != 0 && currentHour >= DisabledAtHour)
                    return false;
            }

            if (IsRemovable && !IsAvailable)
                return false;

            return true;
        }

        [JsonIgnore]
        public bool IsAvailable
        {
            get
            {
                return Directory.Exists(DirectoryPath);
            }
        }
    }

    /// <summary>
    /// Directories to be zipped up and optionally encrypted
    /// </summary>
    public class SourceDirectory : BaseDirectory
    {
        /// <summary>
        /// Only process this directory if the latest file was updated or created this number of minutes 
        /// ago, i.e. let files get this stale before archiving, prevents repeatedly archiving a folder 
        /// if files are updated often and the archiver runs frequently.
        /// </summary>
        public int MinutesOldThreshold { get; set; } = 0;

        /// <summary>
        /// Don't process this source if a task with this name is running, eg. I use Thunderbird which holds on to
        /// the files email are stored in, so have this set to 'Thunderbird' for that sourec directory. Any running task
        /// found with this string in the name will prevent this directory being processed.
        /// </summary>
        public string CheckTaskNameIsNotRunning { get; set; }

        /// <summary>
        /// Indicates whether this source is something that changes a lot, eg source code
        /// as opposed to sets of files that are occasionally added to but not often changed
        /// like movies and photos. This doesn't stop it being archived, but means you can set up 
        /// archive jobs to choose whether to process this source based on the job ArchiveFairlyStatic setting
        /// </summary>
        public bool IsFairlyStatic { get; set; }

        /// <summary>
        /// What compresison to use, see Microsoft System.IO.Compression docs for details at
        /// https://docs.microsoft.com/en-us/dotnet/api/system.io.compression.zipfile.createfromdirectory?view=net-5.0
        /// </summary>
        public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;

        /// <summary>
        /// Overwrite any output files with the same name as one being created
        /// </summary>
        public bool ReplaceExisting { get; set; } = true;

        /// <summary>
        /// Encrypt the output file after zipping, uses AESEncrypt at the moment, so you'd need to install that 
        /// and put the path to the exe in the AESEncryptPath setting in appsettings.json. The reason
        /// it doesn't use built-in .Net encryption is because I use the AESEncrypt Explorer extension
        /// so want to encrypt files with the same code.
        /// </summary>
        public bool EncryptOutput { get; set; } = false;

        /// <summary>
        /// Delete the unencrypted zip archive after successful encryption
        /// </summary>
        public bool DeleteArchiveAfterEncryption { get; set; } = false;

        /// <summary>
        /// Adds a suffix to the file name of the form '-NNNN' before 
        /// the extension, each new file adds 1 to the number. So archiving
        /// 'C:\Blah' results in files like 'Blah-0001.zip', 'Blah-0002.zip' etc.
        /// </summary>
        public bool AddVersionSuffix { get; set; } = false;

        /// <summary>
        /// The name of the zipped output file (no path), if not specified it uses 
        /// the path to generate the name so directory 'C:\AbC\DeF\GhI' will be 
        /// archived to 'AbC-DeF-GhI.zip'
        /// </summary>
        public string OutputFileName { get; set; } = null;
    }

    /// <summary>
    /// Directories for archived files to be copied to from 
    /// the primary archive directory
    /// </summary>
    public class SecureDirectory : BaseDirectory
    {
        /// <summary>
        /// Whether to delete the source file after a successful encrypt, this would
        /// generally be a yes as the whole point is not to leave unencrypted files at 
        /// rest, but the default is not to, so deleting files is a user decision
        /// </summary>
        public bool DeleteSourceAfterEncrypt { get; set; } = false;
    }

    /// <summary>
    /// Directories for archived files to be copied to from 
    /// the primary archive directory
    /// </summary>
    public class ArchiveDirectory : BaseDirectory
    {
    }

    public class JobSpecification
    {
        public string Name { get; set; }

        /// <summary>
        /// Whether the process will write progress information to the console
        /// </summary>
        public bool WriteToConsole { get; set; } = true;

        /// <summary>
        /// Whether the console window will wait for a key to be pressed before closing
        /// </summary>
        public bool PauseBeforeExit { get; set; } = false;

        /// <summary>
        /// Only process those directories that are marked as test ones, for development testing
        /// </summary>
        public bool ProcessTestOnly { get; set; } = false;

        /// <summary>
        /// Use the IsSlowVolume setting on archive directories to decide whether to process 
        /// them or not for this job
        /// </summary>
        public bool ProcessSlowVolumes { get; set; } = false;

        /// <summary>
        /// Whether this configuation archives sources with the IsFairlyStatic setting as true
        /// </summary>
        public bool ArchiveFairlyStatic { get; set; } = false;

        /// <summary>
        /// The place where the primary archives are created according to the set of source directories, which 
        /// are then copied out to the various archive directories, so ideally this is on a large and 
        /// reasonably fast volume.
        /// </summary>
        public string PrimaryArchiveDirectoryName { get; set; }

        /// <summary>
        /// The password to be used for encrypting wit AESEncrypt. Yep, I know, a password in plain text, use 
        /// with caution or just don't use the encrypt facility. 
        /// </summary>
        public string EncryptionPassword { get; set; }

        /// <summary>
        /// Loads the encryption password from this file, opens the file as a text file and takes the content
        /// as the encryption password. If specified, this overrides any value in EncryptionPassword if the fle exists.
        /// </summary>
        public string EncryptionPasswordFile { get; set; }

        /// <summary>
        /// Source, archive and secure directories for this particular job, the global directory
        /// lists are added to these at runtime so each job specification will process it's specific 
        /// directories then the global ones.
        /// </summary>
        public List<SourceDirectory> SourceDirectories { get; set; } = new();
        public List<ArchiveDirectory> ArchiveDirectories { get; set; } = new();
        public List<SecureDirectory> SecureDirectories { get; set; } = new();
    }

    public class Configuration
    {
        /// <summary>
        /// The various different backup jobs, each has it's own 
        /// configuration, eg. for a daily backup, weekly, monthly etc.
        /// </summary>
        public List<JobSpecification> JobSpecifications { get; set; }

        /// <summary>
        /// These directories are added to the directory lists for each backup job
        /// </summary>
        public List<SourceDirectory> GlobalSourceDirectories { get; set; } = new();
        public List<ArchiveDirectory> GlobalArchiveDirectories { get; set; } = new();
        public List<SecureDirectory> GlobalSecureDirectories { get; set; } = new();

        [JsonIgnore]
        internal JobSpecification SelectedJobSpecification { get; private set; }

        internal Result SelectJobSpecification(string jobSpecName)
        {
            Result result = new("SelectJobSpecification");

            SelectedJobSpecification = JobSpecifications.SingleOrDefault(_ => _.Name == jobSpecName);

            if (SelectedJobSpecification is not null)
            {
                SelectedJobSpecification.SourceDirectories.AddRange(GlobalSourceDirectories);
                SelectedJobSpecification.ArchiveDirectories.AddRange(GlobalArchiveDirectories);
                SelectedJobSpecification.SecureDirectories.AddRange(GlobalSecureDirectories);

                if (!string.IsNullOrEmpty(SelectedJobSpecification.EncryptionPasswordFile))
                {
                    if (!string.IsNullOrEmpty(SelectedJobSpecification.EncryptionPassword))
                    {
                        result.AddWarning($"EncryptionPasswordFile will override EncryptionPassword value for job {SelectedJobSpecification.Name}");
                    }

                    if (File.Exists(SelectedJobSpecification.EncryptionPasswordFile))
                    {
                        SelectedJobSpecification.EncryptionPassword = File.ReadAllText(SelectedJobSpecification.EncryptionPasswordFile);
                    }
                }
                else
                {
                    result.AddError($"EncryptionPasswordFile '{SelectedJobSpecification.EncryptionPasswordFile}' does not exist");
                }
            }
            else
            {
                result.AddError($"cabnnot find job specificatrion '{jobSpecName}'");
            }

            return result;
        }
    }
}
