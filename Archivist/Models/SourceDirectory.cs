using System.IO.Compression;

namespace Archivist.Models
{
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
}