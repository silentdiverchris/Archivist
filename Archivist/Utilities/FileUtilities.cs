using Archivist.Classes;
using System.Text.RegularExpressions;

namespace Archivist.Utilities
{
    internal static class FileUtilities
    {
        internal static DriveInfo GetDriveByLabel(string label)
        {
            var drives = DriveInfo.GetDrives();

            DriveInfo found = null; // drives.SingleOrDefault(_ => _.VolumeLabel == label);

            // If we use linq we throw an exception if one of the drives isn't formatted, so...

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
                    // corrupted USB stick, next time I get a duff drive I'll see about testing this path TODO

                    // Either way, there's something wrong with the volume, we don't
                    // really care what, for sure it's not one we can use so just ignore...
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

        internal static string GetByteSizeAsText(double sizeBytes)
        {
            if (sizeBytes > (1024 * 1024 * 1024))
            {
                return $"{(sizeBytes / 1024 / 1024 / 1024):N2} GB";
            }
            else if (sizeBytes > (1024 * 1024))
            {
                return $"{(sizeBytes / 1024 / 1024):N2} MB";
            }
            else if (sizeBytes > 1024)
            {
                return $"{(sizeBytes / 1024):N2} KB";
            }
            else
            {
                return $"{sizeBytes:N0} bytes";
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
                    : $", volume '{volumeLabel}'";

                string freeSpaceText = $"Remaining space on drive {drive[0]}{volLabStr} is {FileUtilities.GetByteSizeAsText(bytesFree)}";

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

        internal static bool IsLastWrittenMoreThanDaysAgo(string filePath, int daysAgo, out DateTime lastWriteTimeUtc)
        {
            if (File.Exists(filePath))
            {
                var fi = new FileInfo(filePath);

                lastWriteTimeUtc = fi.LastWriteTimeUtc;

                return fi.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-1 * daysAgo);
            }
            else
            {
                throw new Exception($"IsLastWrittenMoreThanDaysAgo given non-existant file {filePath}");
            }
        }

        internal static bool IsLastWrittenLessThanDaysAgo(string fileName, int daysAgo, out DateTime lastWriteTimeUtc)
        {
            return !IsLastWrittenMoreThanDaysAgo(fileName, daysAgo, out lastWriteTimeUtc);
        }
    }
}
