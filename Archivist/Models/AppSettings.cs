using Archivist.Classes;
using Archivist.Helpers;
using Archivist.Utilities;
using System.IO.Compression;
using System.Text.Json.Serialization;

namespace Archivist.Models
{
    public class AppSettings
    {
        public string DefaultJobName { get; set; }
        public string LogDirectoryPath { get; set; }
        public string AESEncryptPath { get; set; }
        public string SqlConnectionString { get; set; }
        
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
        public string LoadedFromFile { get; set; }

        [JsonIgnore]
        internal Job SelectedJob { get; private set; } = null;

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

                    foreach (var dir in SelectedJob.SourceDirectories)
                    {
                        dir.VerifyVolume();
                    }

                    foreach (var dir in SelectedJob.ArchiveDirectories)
                    {
                        dir.VerifyVolume();
                    }

                    foreach (var dir in SelectedJob.SecureDirectories)
                    {
                        dir.VerifyVolume();
                    }

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
                        result.AddError($"cannot find job specification '{jobName}', available names are {Jobs.Select(_ => _.Name).ConcatenateToDelimitedList()}, loaded from '{LoadedFromFile}'");
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
        /// <summary>
        /// If non-null, find a drive with this label and combine with the DirectoryPath 
        /// to determine the directory to be used. Allows for removable dribes that get 
        /// mounted with different letters to be identified.
        /// If the DirectoryPath has a drive designation, eg 'D:\Archive', this property 
        /// is ignored (specifically characters 2 and 3 are ':\'), so to use this function, set
        /// this to, say, '4TB External HDD' and the DirectoryPath to 'Archive'
        /// </summary>
        public string VolumeLabel { get; set; }

        /// <summary>
        /// The full path of this directory
        /// </summary>
        public string DirectoryPath { get; set; }

        /// <summary>
        /// Set the creation and last write time of any files created to the same as the source
        /// </summary>
        public bool SynchoniseFileTimestamps { get; set; } = true;

        /// <summary>
        /// Whether to delete the source file after a successful encrypt, this would
        /// generally be a yes as the whole point is not to leave unencrypted files at 
        /// rest, but the default is not to, so deleting files is a user decision
        /// </summary>
        public bool DeleteSourceAfterEncrypt { get; set; } = false;

        /// <summary>
        /// A human description of what this directory is, or what drive it's 
        /// on - eg '128gb USB Key' or 'Where my photos are', this doesn't make anything work or
        /// break anything if left undefined, it's just a reminder for the human.
        /// </summary>
        public string Description { get; set; }

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
        /// Marks this directory as one to process when the backup type has ProcessTestOnly set, this
        /// is just used for developing the code
        /// </summary>
        public bool IsForTesting { get; set; } = false;

        /// <summary>
        /// Whether this is a slow volume, used in conjunction with config WriteToSlowVolumes so
        /// backup jobs that only read from and write to fast drives can be set up by setting job 
        /// setting ProcessSlowVolumes to false.
        /// </summary>
        public bool IsSlowVolume { get; set; } = false;

        private bool _isAvailable = false;

        [JsonIgnore]
        public bool IsAvailable
        {
            get
            {
                return _isAvailable;
            }
        }

        public void VerifyVolume()
        {
            _isAvailable = false; // That's the default but just in case

            if (!string.IsNullOrEmpty(VolumeLabel))
            {
                if (!DirectoryPath.Contains(@":\"))
                {
                    DriveInfo drive = FileUtilities.GetDriveByLabel(VolumeLabel);

                    if (drive is not null)
                    {
                        DirectoryPath = Path.Join(drive.Name, DirectoryPath);

                        _isAvailable = Directory.Exists(DirectoryPath);
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
                else
                {
                    throw new Exception($"IdentifyVolume found VolumeLabel '{VolumeLabel}' but DirectoryPath '{DirectoryPath}' has a nominated drive");
                }
            }
            else
            {
                _isAvailable = Directory.Exists(DirectoryPath);
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

            return true;
        }
    }

    /// <summary>
    /// Properies that relate to archiving and copying files
    /// </summary>
    public class BaseDirectoryFiles : BaseDirectory
    {
        /// <summary>
        /// If a file has a version suffix (created by setting source directory setting AddVersionSuffix 
        /// to true) it will retain at least this many of them in this directory, zero means we keep all versions, which
        /// will eventually fill the volume. One gotcha is that if you set this lower on an archive directory
        /// than on the source directory the system will keep copying over older versions and then deleting 
        /// them, watch out for that.
        /// </summary>
        public int RetainMinimumVersions { get; set; } = Constants.RETAIN_VERSIONS_MINIMUM;

        /// <summary>
        /// If a file has a version suffix (created by setting source directory setting AddVersionSuffix 
        /// to true) it will retain a maximum this many of them in this directory, zero means we keep all versions, which
        /// will eventually fill the volume. One gotcha is that if you set this lower on an archive directory
        /// than on the source directory the system will keep copying over older versions and then deleting 
        /// them, watch out for that.
        /// </summary>
        public int RetainMaximumVersions { get; set; } = 3;

        /// <summary>
        /// If a file has a version suffix (created by setting source directory setting AddVersionSuffix 
        /// to true) it will retain files that were written less than this many days ago, this overrides
        /// the retainVersions setting. Zero disables this function.
        /// </summary>
        public int RetainYoungerThanDays { get; set; } = Constants.RETAIN_YOUNGER_THAN_DAYS_MINIMUM;

        /// <summary>
        /// A list of file specs to include, eg "*.txt', 'thing.*', abc???de.jpg' etc.
        /// An empty list includes all files
        /// </summary>
        public List<string> IncludeSpecifications { get; set; }

        [JsonIgnore]
        public string IncludeSpecificationsText => IncludeSpecifications.Any()
            ? IncludeSpecifications.ConcatenateToDelimitedList()
            : "all files";

        /// <summary>
        /// A list of file specs to ignore, eg "*.txt', 'thing.*', abc???de.jpg' etc. 
        /// An empty list doesn't exclude any files
        /// </summary>
        public List<string> ExcludeSpecifications { get; set; }

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
    }

    public class Job
    {
        /// <summary>
        /// The name of this job, cannot contain spaces
        /// </summary>
        public string Name { get; set; }

        // For humans only, a description of what this job does, not validated in any way
        public string Description { get; set; }

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
}