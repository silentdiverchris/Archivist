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

        internal static Result CheckDiskSpace(string directoryName)
        {
            Result result = new("CheckDiskSpace", false);

            string drive = Path.GetPathRoot(directoryName);

            DriveInfo di = new(drive);

            double gbFree = di.TotalFreeSpace;

            const double threshold = 50L * 1024 * 1024 * 1024;

            string freeSpaceText = $"Remaining space on drive {drive[0]} is {FileUtilities.GetByteSizeAsText(gbFree)}";

            if (gbFree < (threshold))
            {
                result.AddWarning(freeSpaceText);
            }
            else
            {
                result.AddInfo(freeSpaceText);
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
