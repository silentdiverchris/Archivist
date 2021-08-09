using Archivist.Classes;
using System.IO;

namespace Archivist.Helpers
{
    internal static class FileHelpers
    {
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

            string freeSpaceText = $"Remaining disk space on drive {drive[0]} is {FileHelpers.GetByteSizeAsText(gbFree)}";

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
    }
}
