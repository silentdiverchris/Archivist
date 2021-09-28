using Archivist.Classes;
using Archivist.Helpers;
using Archivist.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

    /// <summary>
    /// Properties that relate specifically to a directory
    /// </summary>
    public class BaseDirectory
    {
        private bool _pathInitialised = false;
        private string? _directoryPath = null;

        /// <summary>
        /// If non-null, find a drive with this label and combine with the DirectoryPath 
        /// to determine the directory to be used. Allows for removable dribes that get 
        /// mounted with different letters to be identified.
        /// If the DirectoryPath has a drive designation, eg 'D:\Archive', this property 
        /// is ignored (specifically characters 2 and 3 are ':\'), so to use this function, set
        /// this to, say, '4TB External HDD' and the DirectoryPath to 'Archive'
        /// </summary>
        public string? VolumeLabel { get; set; }

        /// <summary>
        /// The path of this directory, soem directories are identified by a path name 
        /// and volume label rather than drive letter, so this sets up the drive letter 
        /// on first use where necessary
        /// </summary>
        /// 
        public string? DirectoryPath
        {            
            get
            {
                if (!_pathInitialised)
                {
                    // Set this first or the VerifyVolume call will recurse
                    _pathInitialised = true;

                    if (VolumeLabel is not null)
                    {
                        VerifyVolume();
                    }
                }

                return _directoryPath;
            }
            set
            {
                _directoryPath = value;
            }
        }

        /// <summary>
        /// A human description of what this directory is, or what drive it's 
        /// on - eg '128gb USB Key' or 'Where my photos are', this doesn't make anything work or
        /// break anything if left undefined, it's just a reminder for the human.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Whether to process this directory in any way
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Whether this is on a volume that is removeable, mainly determining whether it should 
        /// be considered an error if the volume it's on can't be found. If it can't be found it
        /// won't even be considered as a warning if this is true.
        /// </summary>
        public bool IsRemovable { get; set; } = false;

        /// <summary>
        /// Process directories in this order, lowest number first (then by directory name alpha)
        /// </summary>
        public short Priority { get; set; } = 99;

        /// <summary>
        /// Only process this directory after this hour starts, zero disables
        /// </summary>
        public short EnabledAtHour { get; set; } = 0;

        /// <summary>
        /// Only process this directory before this hour starts, zero disables
        /// </summary>
        public short DisabledAtHour { get; set; } = 0;

        /// <summary>
        /// Whether this is a slow volume, used in conjunction with config WriteToSlowVolumes so
        /// backup jobs that only read from and write to fast drives can be set up by setting job 
        /// setting ProcessSlowVolumes to false.
        /// </summary>
        public bool IsSlowVolume { get; set; } = false;

        /// <summary>
        /// Whether this directory was found to exist and be readable
        /// </summary>
        [JsonIgnore]
        public bool IsAvailable
        {
            get
            {
                return Directory.Exists(DirectoryPath);
            }
        }

        public void VerifyVolume()
        {
            if (!string.IsNullOrEmpty(VolumeLabel))
            {
                if (DirectoryPath!.Contains(@":\") == false)
                {
                    DriveInfo? drive = FileUtilities.GetDriveByLabel(VolumeLabel);

                    if (drive is not null)
                    {
                        DirectoryPath = Path.Join(drive.Name, DirectoryPath);
                    }
                    else
                    {
                        if (IsRemovable)
                        {
                            // That's fine, it's not mounted
                        }
                        else
                        {
                            throw new Exception($"IdentifyVolume cannot find volume '{VolumeLabel}'");
                        }
                    }
                }
                //else
                //{
                //    throw new Exception($"IdentifyVolume found VolumeLabel '{VolumeLabel}' but DirectoryPath '{DirectoryPath}' has a nominated drive");
                //}
            }
        }

        public bool IsToBeProcessed(Job jobSpec)
        {
            if (!IsEnabled)
                return false;

            if (IsRemovable && !IsAvailable)
                return false;

            if (IsSlowVolume && jobSpec.ProcessSlowVolumes == false)
                return false;

            if (EnabledAtHour != 0 || DisabledAtHour != 0)
            {
                var currentHour = DateTime.Now.Hour;

                if (EnabledAtHour != 0 && currentHour < EnabledAtHour)
                    return false;

                if (DisabledAtHour != 0 && currentHour >= DisabledAtHour)
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Properies that relate to archiving and copying files
    /// </summary>
    public class BaseDirectoryFiles : BaseDirectory
    {
        public BaseDirectoryFiles GetBase()
        {
            return this;
        }

        /// <summary>
        /// Retain a maximum of this many versions in this directory, zero means we keep all versions, which
        /// will eventually fill the volume. One gotcha is that if you set this lower on an archive directory
        /// than on the source directory the system will keep copying over older versions and then deleting 
        /// them, watch out for that.
        /// </summary>
        public int RetainMaximumVersions { get; set; } = Constants.RETAIN_VERSIONS_MINIMUM;

        /// <summary>
        /// Retain files that were written less than this many days ago, this overrides
        /// the RetainMaximumVersions setting. Zero disables this function.
        /// </summary>
        public int RetainYoungerThanDays { get; set; } = Constants.RETAIN_DAYS_OLD_MINIMUM;

        /// <summary>
        /// A list of file specs to include, eg "*.txt', 'thing.*', abc???de.jpg' etc.
        /// An empty list includes all files
        /// </summary>
        public List<string> IncludeSpecifications { get; set; } = new();

        [JsonIgnore]
        public string IncludeSpecificationsText => IncludeSpecifications.Any()
            ? IncludeSpecifications.ConcatenateToDelimitedList()
            : "all files";

        /// <summary>
        /// A list of file specs to ignore, eg "*.txt', 'thing.*', abc???de.jpg' etc. 
        /// An empty list doesn't exclude any files
        /// </summary>
        public List<string> ExcludeSpecifications { get; set; } = new();

        [JsonIgnore]
        public string ExcludeSpecificationsText => ExcludeSpecifications.Any()
            ? ExcludeSpecifications.ConcatenateToDelimitedList()
            : "nothing";
    }

    /// <summary>
    /// Directories to be zipped up to the primary archive directory and 
    /// optionally encrypted
    /// </summary>
    public class SourceDirectory : BaseDirectoryFiles
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
        public string? CheckTaskNameIsNotRunning { get; set; }

        /// <summary>
        /// What compresison to use, see Microsoft System.IO.Compression docs for details at
        /// https://docs.microsoft.com/en-us/dotnet/api/system.io.compression.zipfile.createfromdirectory?view=net-5.0
        /// </summary>
        public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;

        /// <summary>
        /// Encrypt the output file after zipping, uses AESEncrypt at the moment, so you'd need to install that 
        /// and put the path to the exe in the AESEncryptPath setting in appsettings.json. The reason
        /// it doesn't use built-in .Net encryption is because I use the AESEncrypt Explorer extension
        /// so want to encrypt files with the same code. If this is true, the unencrypted version will 
        /// always be deleted after the encryption is completed. If for some reason the encryption fails, the
        /// unencrypted version will not be deleted.
        /// </summary>
        public bool EncryptOutput { get; set; } = false;
    }

    /// <summary>
    /// Directories where files are to be individually encrypted
    /// </summary>
    public class SecureDirectory : BaseDirectory
    {
    }

    /// <summary>
    /// Directories for archived files to be copied to from 
    /// the primary archive directory
    /// </summary>
    public class ArchiveDirectory : BaseDirectoryFiles
    {
        [JsonIgnore]
        public DirectoryStatistics Statistics { get; set; } = new();
    }

    public class Job
    {
        public DirectoryStatistics PrimaryArchiveStatistics { get; set; } = new();

        /// <summary>
        /// The name of this job, cannot contain spaces
        /// </summary>
        public string? Name { get; set; }

        // For humans only, a description of what this job does, not validated in any way
        public string? Description { get; set; }

        /// <summary>
        /// If logging to file is enabled, open the new log file in the associated 
        /// application for a .log file (notepad or whatever)
        /// </summary>
        public bool AutoViewLogFile { get; set; } = false;

        /// <summary>
        /// Whether the process will write progress information to the console
        /// </summary>
        public bool WriteToConsole { get; set; } = true;

        /// <summary>
        /// Whether the console window will wait for a key to be pressed before closing
        /// </summary>
        public bool PauseBeforeExit { get; set; } = false;

        /// <summary>
        /// Use the IsSlowVolume setting on archive directories to decide whether to process 
        /// them or not for this job
        /// </summary>
        public bool ProcessSlowVolumes { get; set; } = false;

        /// <summary>
        /// The place where the primary archives are created according to the set of source directories, which 
        /// are then copied out to the various archive directories, so ideally this is on a large and 
        /// reasonably fast volume.
        /// </summary>
        public string? PrimaryArchiveDirectoryPath { get; set; }

        /// <summary>
        /// The password to be used for encrypting wit AESEncrypt. Yep, I know, a password in plain text, use 
        /// with caution or just don't use the encrypt facility. 
        /// </summary>
        public string? EncryptionPassword { get; set; }

        /// <summary>
        /// Loads the encryption password from this file, opens the file as a text file and takes the content
        /// as the encryption password. If specified, this overrides any value in EncryptionPassword if the fle exists.
        /// </summary>
        public string? EncryptionPasswordFile { get; set; }

        /// <summary>
        /// Source, archive and secure directories for this particular job, the global directory
        /// lists are added to these at runtime so each job specification will process it's specific 
        /// directories then the global ones.
        /// </summary>
        public List<SourceDirectory> SourceDirectories { get; set; } = new();
        public List<ArchiveDirectory> ArchiveDirectories { get; set; } = new();
        public List<SecureDirectory> SecureDirectories { get; set; } = new();
    }
}