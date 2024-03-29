﻿using Archivist.Classes;
using Archivist.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Archivist.Utilities
{
    internal static class FileUtilities
    {
        /// <summary>
        /// Round the creation and last write times in all files in 
        /// a directory with one of the matching specifications
        /// </summary>
        /// <param name="directoryPath"></param>
        internal static void RoundDirectoryTimes(string directoryPath, List<string> fileSpecifications)
        {
            if (Directory.Exists(directoryPath))
            {
                foreach (var fileSpec in fileSpecifications)
                {
                    foreach (var fileName in Directory.GetFiles(directoryPath, fileSpec))
                    {
                        RoundFileTimes(fileName);
                    }
                }
            }
        }

        /// <summary>
        /// Round the LastWriteTime and CreationTime to the nearest whole second.
        /// The reason being, when we copy files around we synchronise last write and 
        /// create timestamps, but with file systems like exFAT we lose some accuracy 
        /// as compared to NTFS, say, so when we compare timestamps they don't match. 
        /// Having the timestamps as exact seconds means files created on NTFS and 
        /// copied to exFAT or whatever format have identical timestamps.
        /// </summary>
        /// <param name="fullFileName"></param>
        internal static void RoundFileTimes(string fullFileName)
        {
            var fi = new FileInfo(fullFileName);

            if (fi.Exists)
            {
                var lwt = fi.LastWriteTime;
                var crt = fi.CreationTime;

                var lwtr = lwt.Floor(new TimeSpan(0,0,1));
                var crtr = crt.Floor(new TimeSpan(0, 0, 1));

                if (fi.LastWriteTime != lwtr)
                {
                    fi.LastWriteTime = lwtr;
                }

                if (fi.CreationTime != crtr)
                {
                    fi.CreationTime = crtr;
                }
            }
            else
            {
                throw new Exception($"RoundFileTimes file '{fullFileName}' does not exist");
            }
        }

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

            // If we use linq to do this an exception is thrown if one of the drives isn't
            // readable but we don't get told which, so...

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
                    // what, the fact we are here means it's not one we're going to
                    // get a label for, so just ignore it.
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

                const double threshold = 20L * 1024 * 1024 * 1024;

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
        /// We have a couple of out parameters, since we have to get a FileInfo here and at least 
        /// one caller wants the last write and the length just after the call, we might as well get 
        /// the data and hand it back to them, rather than they create another FileInfo
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
