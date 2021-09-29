using Archivist.Classes;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Archivist.Utilities
{
    internal static class FileUtilities
    {
        /// <summary>
        /// Generate the base output file name for this source directory, that is, the
        /// file name without any extension. The final file name will be given 
        /// a .zip or .aes extension as required
        /// </summary>
        /// <param name="directoryName"></param>
        /// <returns></returns>
        internal static string GenerateBaseOutputFileName(SourceDirectory sourceDirectory)
        {
            var dirNames = sourceDirectory.DirectoryPath!.Split(Path.DirectorySeparatorChar);

            if (dirNames.Length > 1)
            {
                var fileName = string.Join("-", dirNames[1..]);
                return fileName;
            }
            else
            {
                throw new Exception($"GenerateFileNameFromPath found path {sourceDirectory.DirectoryPath} too short");
            }
        }

        internal static DriveInfo? GetDriveByLabel(string? label)
        {
            var drives = DriveInfo.GetDrives();

            DriveInfo? found = null; // drives.SingleOrDefault(_ => _.VolumeLabel == label);

            // If we use linq an exception is thrown if one of the drives isn't readable, so...

            foreach (var drv in drives)
            {
                try
                {
                    if (drv.VolumeLabel == label)
                    {
                        found = drv;
                        break;
                    }
                }
                catch
                {
                    // There's something wrong with the volume, we don't really care
                    // what, it's certainly not one we can get a label from or use so
                    // just ignore it.
                }
            }

            return found;
        }

        internal static Regex GenerateRegexForFileMask(this string fileMask)
        {
            Regex mask = new(
                "^.*" +
                fileMask
                    .Replace(".", "[.]")
                    .Replace("*", ".*")
                    .Replace("?", ".")
                + '$',
                RegexOptions.IgnoreCase);

            return mask;
        }

        internal static string GetByteSizeAsText(double sizeBytes, bool exact = false)
        {
            if (exact)
            {
                return $"{sizeBytes:N0} bytes";
            }
            else
            {
                if (sizeBytes >= (1024 * 1024 * 1024))
                {
                    return $"{(sizeBytes / 1024 / 1024 / 1024):N2}GB";
                }
                else if (sizeBytes >= (1024 * 1024))
                {
                    return $"{(sizeBytes / 1024 / 1024):N2}MB";
                }
                else if (sizeBytes >= 1024)
                {
                    return $"{(sizeBytes / 1024):N2}KB";
                }
                else
                {
                    return $"<1KB";
                }
            }
        }

        internal static double GetAvailableDiskSpace(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                string drive = Path.GetPathRoot(directoryPath)!;

                DriveInfo di = new(drive);

                double free = di.TotalFreeSpace;

                return free;
            }
            else
            {
                return -1;
            }
        }

        internal static Result CheckDiskSpace(string directoryPath, string? volumeLabel = null)
        {
            Result result = new("CheckDiskSpace", false);

            if (Directory.Exists(directoryPath))
            {
                string drive = Path.GetPathRoot(directoryPath)!;

                DriveInfo di = new(drive);

                double bytesFree = di.TotalFreeSpace;

                const double threshold = 50L * 1024 * 1024 * 1024;

                string? volLabStr = volumeLabel is null
                    ? null
                    : $" '{volumeLabel}'";

                string freeSpaceText = $"Remaining space on drive {drive[0]}{volLabStr} for '{directoryPath}' is {FileUtilities.GetByteSizeAsText(bytesFree)}";

                if (bytesFree < threshold)
                {
                    result.AddWarning(freeSpaceText);
                }
                else
                {
                    result.AddInfo(freeSpaceText);
                }
            }
            else
            {
                result.AddError($"Directory {directoryPath} does not exist");
            }

            return result;
        }

        /// <summary>
        /// We have a couple of out parrameters, since we have to get a FileInfo here and at least 
        /// one caller wants the last write and the length just after the call, we might as well get 
        /// the data for them rather than they create another FileInfo
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="daysAgo"></param>
        /// <param name="lastWriteTimeLocal"></param>
        /// <param name="fileLength"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        internal static bool IsYoungerThanDays(string filePath, int daysAgo, out DateTime lastWriteTimeLocal, out long fileLength)
        {
            if (File.Exists(filePath))
            {
                var fi = new FileInfo(filePath);

                lastWriteTimeLocal = fi.LastWriteTime;
                fileLength = fi.Length;

                var thresholdLocal = DateTime.Now.AddDays(-1 * daysAgo);

                bool younger = lastWriteTimeLocal > thresholdLocal;

                return younger;
            }
            else
            {
                throw new Exception($"IsYoungerThanDays given non-existant file {filePath}");
            }
        }

        internal static bool IsOlderThanDays(string filePath, int daysAgo, out DateTime lastWriteTimeLocal, out long fileLength)
        {
            return !IsYoungerThanDays(filePath, daysAgo, out lastWriteTimeLocal, out fileLength);
        }
    }
}
