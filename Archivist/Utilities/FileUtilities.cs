using Archivist.Classes;
using System.Text.RegularExpressions;

namespace Archivist.Utilities
{
    internal static class FileUtilities
    {
        internal static DriveInfo GetDriveByLabel(string label)
        {
            return DriveInfo.GetDrives().SingleOrDefault(_ => _.VolumeLabel == label);
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
            else if(sizeBytes > (1024 * 1024))
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

                double gbFree = di.TotalFreeSpace;

                const double threshold = 50L * 1024 * 1024 * 1024;

                string volLabStr = volumeLabel is null
                    ? null
                    : $", volume '{volumeLabel}'";

                string freeSpaceText = $"Remaining space on drive {drive[0]}{volLabStr} is {FileUtilities.GetByteSizeAsText(gbFree)}";

                if (gbFree < (threshold))
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

        internal static bool IsLastWrittenMoreThanDaysAgo(string fileName, int daysAgo)
        {
            var fi = new FileInfo(fileName);

            return fi.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-1 * daysAgo);
        }

        internal static bool IsLastWrittenLessThanDaysAgo(string fileName, int daysAgo)
        {
            return !IsLastWrittenMoreThanDaysAgo(fileName, daysAgo);
        }
    }
}
