using Archivist.Classes;
using Archivist.Helpers;
using System.Text.RegularExpressions;

namespace Archivist.Utilities
{
    internal static class FileUtilities
    {
        /// <summary>
        /// Matches the pattern for a versioned file, i.e. ends with '-NNNN.xxx' where the Ns are numeric
        /// </summary>
        /// <param name="fileName">Can be just the file name or a full path</param>
        /// <param name="rootName"></param>
        /// <returns></returns>
        internal static bool IsFileVersioned(string fileName, out string rootName)
        {
            bool isVersioned = false;

            try
            {
                // Get just file name of the file without any path

                if (fileName.Contains(Path.DirectorySeparatorChar))
                {
                    fileName = fileName[(fileName.LastIndexOf(Path.DirectorySeparatorChar) + 1)..];
                }

                // Any versioned file name will be over 9 characters long as the version suffix and extension
                // are 9 in themselves

                if (fileName.Length > 9)
                {
                    // Faster than a regex and/or using a FileInfo to get the extension and we need to check the digits anyway
                    string hyphen = fileName.Substring(fileName.Length - 9, 1);
                    string numbers = fileName.Substring(fileName.Length - 8, 4);
                    string dot = fileName.Substring(fileName.Length - 4, 1);

                    isVersioned = hyphen == "-" && dot == "." && StringHelpers.IsDigits(numbers);

                    if (isVersioned)
                    {
                        rootName = fileName[0..^9] + fileName[^4..];
                    }
                    else
                    {
                        rootName = fileName;
                    }
                }
                else
                {
                    rootName = fileName;
                }

                return isVersioned;
            }
            catch (Exception ex)
            {
                throw new Exception($"FileNameMatchesVersionedPattern file '{fileName}' exception {ex.Message}", ex);
            }
        }

        internal static DriveInfo GetDriveByLabel(string label)
        {
            var drives = DriveInfo.GetDrives();

            DriveInfo found = null; // drives.SingleOrDefault(_ => _.VolumeLabel == label);

            // If we use linq an exception is thrown if one of the drives isn't formatted, so...

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
                catch  // (Exception drvEx)
                {
                    // if (drvEx.) ... how to reproduce the error that brought us to capture this ? - it was a
                    // corrupted USB stick, next time I get a duff drive I'll see about testing this path. TODO

                    // Either way, there's something wrong with the volume, we don't
                    // really care what, it's not one we can use so just ignore it.
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
                string drive = Path.GetPathRoot(directoryPath);

                DriveInfo di = new(drive);

                double free = di.TotalFreeSpace;

                return free;
            }
            else
            {
                return -1;
            }
        }

        internal static Result CheckDiskSpace(string directoryPath, string volumeLabel = null)
        {
            Result result = new("CheckDiskSpace", false);

            if (Directory.Exists(directoryPath))
            {
                string drive = Path.GetPathRoot(directoryPath);

                DriveInfo di = new(drive);

                double bytesFree = di.TotalFreeSpace;

                const double threshold = 50L * 1024 * 1024 * 1024;

                string volLabStr = volumeLabel is null
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

        internal static bool IsLastWrittenMoreThanDaysAgo(string filePath, int daysAgo, out DateTime lastWriteTimeLocal)
        {
            if (File.Exists(filePath))
            {
                var fi = new FileInfo(filePath);

                lastWriteTimeLocal = fi.LastWriteTime;

                return fi.LastWriteTime < DateTime.Now.AddDays(-1 * daysAgo);
            }
            else
            {
                throw new Exception($"IsLastWrittenMoreThanDaysAgo given non-existant file {filePath}");
            }
        }

        internal static bool IsLastWrittenLessThanDaysAgo(string fileName, int daysAgo, out DateTime lastWriteTime)
        {
            return !IsLastWrittenMoreThanDaysAgo(fileName, daysAgo, out lastWriteTime);
        }
    }
}
