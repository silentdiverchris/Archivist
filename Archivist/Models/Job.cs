using Archivist.Classes;
using System.Collections.Generic;

namespace Archivist.Models
{
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