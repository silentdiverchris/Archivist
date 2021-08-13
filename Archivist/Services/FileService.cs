using Archivist.Classes;
using Archivist.Helpers;
using Archivist.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Archivist.Services
{
    internal class FileService : BaseService
    {
        internal int TotalFilesCopied { get; private set; } = 0;
        internal long TotalBytesCopied { get; private set; } = 0;

        internal FileService(
            JobSpecification jobSpec,
            LogService logService) : base(jobSpec, logService)
        {
        }

        internal async Task<Result> DeleteOldVersions(string latestFileName, int retainVersions, int retainDaysOld)
        {
            // We shouldn't even be in here if RetainVersions isn't at least the minimum but just in case...
            if (retainVersions < Constants.RETAIN_VERSIONS_MINIMUM)
            {
                throw new Exception($"DeleteOldVersions invalid retainVersions {retainVersions} for '{latestFileName}'");
            }

            Result result = new("DeleteOldVersions", true, $"for file '{latestFileName}'");

            string retainStr = retainVersions > 0
                ? $"Retaining the last {retainVersions} versions" + (retainDaysOld > 0
                    ? $" and all files under {retainDaysOld} days old"
                    : "")
                : "";

            result.AddDebug(retainStr);

            await _logService.ProcessResult(result);

            try
            {
                // The suffix is of the form -nnnn.zip, so for file abcde.zip we are looking for abcde-nnnnn.zip

                FileInfo fiArchive = new(latestFileName);

                string fileNamePrefix = fiArchive.Name.Substring(0, fiArchive.Name.Length - 9);
                string searchPattern = $"{fileNamePrefix}*.zip";

                var existingFiles = Directory.GetFiles(fiArchive.DirectoryName, searchPattern)
                    .Where(_ => _.Length == latestFileName.Length)
                    .OrderBy(_ => _);

                if (existingFiles.Any())
                {
                    if (existingFiles.Count() > retainVersions)
                    {
                        if (latestFileName == existingFiles.Last())
                        {
                            foreach (var fileName in existingFiles)
                            {                                
                                if (FileNameMatchesVersionedPattern(fileName) == false)
                                {
                                    result.AddError($"DeleteOldVersions found {fileName} not matching pattern");
                                }
                            }

                            if (result.HasNoErrorsOrWarnings)
                            {
                                // OK, it's safe, all the files look right, delete the excess....

                                var filesToDelete = existingFiles.Take(existingFiles.Count() - retainVersions);

                                foreach (var fileName in filesToDelete)
                                {
                                    // Last second paranoia, whatever bug might get introduced above, never
                                    // delete the one we just created !

                                    if (fileName != latestFileName)
                                    {
                                        if (FileHelpers.IsLastWrittenMoreThanDaysAgo(fileName, retainDaysOld))
                                        {
                                            result.AddInfo($"Deleting version '{fileName}'");
                                            File.Delete(fileName);
                                        }
                                        else
                                        {
                                            result.AddInfo($"Retaining version '{fileName}' because it was last written less than {retainDaysOld} days ago");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        result.AddInfo($"DeleteOldVersions for '{latestFileName}' found {existingFiles.Count()} version{existingFiles.Count().PluralSuffix()}, which is OK");
                    }
                }
            }
            catch (Exception ex)
            {
                result.AddException(ex);
            }

            await _logService.ProcessResult(result);

            return result;
        }

        /// <summary>
        /// Matches the pattern for a versioned file, i.e. ends with '-NNNN.xxx' where the Ns are numeric
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        internal static bool FileNameMatchesVersionedPattern(string fileName)
        {
            if (fileName.Length > 10)
            {
                // Replace with a regex? TODO
                string hyphen = fileName.Substring(fileName.Length - 9, 1);
                string numbers = fileName.Substring(fileName.Length - 8, 4);
                string dot = fileName.Substring(fileName.Length - 4, 1);

                return hyphen == "-" && dot == "." && StringHelpers.IsDigits(numbers);
            }
            else
            {
                return false;
            }
        }

        internal async Task<Result> CopyToArchives()
        {
            Result result = new("CopyToArchives", true);

            foreach (var destination in _jobSpec.ArchiveDirectories
                .Where(_ => _.IsToBeProcessed(_jobSpec))
                .OrderBy(_ => _.Priority)
                .ThenBy(_ => _.DirectoryPath))
            {
                Result copyResult = await CopyFilesAsync(_jobSpec.PrimaryArchiveDirectoryName, destination);

                result.SubsumeResult(copyResult);

                if (result.HasErrors)
                    break;
            }

            await _logService.ProcessResult(result, addCompletionItem: true, reportItemCounts: true, "file");

            return result;
        }

        /// <summary>
        /// Copies from one directory to another, NOT RECURSIVE
        /// </summary>
        /// <param name="sourceDirectoryName"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        internal async Task<Result> CopyFilesAsync(string sourceDirectoryName, ArchiveDirectory destination)
        {
            Result result = new(
                functionName: "CopyFilesAsync",
                addStartingItem: true,
                appendText: $"from '{sourceDirectoryName}' to '{destination.DirectoryPath}' including {destination.IncludeSpecificationsText}, excluding {destination.ExcludeSpecificationsText}");

            var diSrc = new DirectoryInfo(sourceDirectoryName);
            var diDest = new DirectoryInfo(destination.DirectoryPath);

            if (destination.IsRemovable)
            {
                string drive = Path.GetPathRoot(destination.DirectoryPath);

                if (!Directory.Exists(drive))
                {
                    result.AddInfo($"Removable destination drive {drive.Substring(0, 1)} is not mounted, skipping");
                    await _logService.ProcessResult(result);
                    return result;
                }
            }

            if (!diDest.Exists)
            {
                Directory.CreateDirectory(destination.DirectoryPath);
                diDest = new DirectoryInfo(destination.DirectoryPath);

                if (!diDest.Exists)
                {
                    if (destination.IsRemovable)
                    {
                        result.AddWarning($"Removable destination directory {destination.DirectoryPath} does not exist and cannot be created");
                    }
                    else
                    {
                        result.AddError($"Non-removable destination directory {destination.DirectoryPath} does not exist and cannot be created");
                    }
                }
            }

            if (diSrc.Exists)
            {
                // NOT RECURSIVE

                result.ItemsFound = Directory.GetFiles(sourceDirectoryName, searchPattern: "*.*", searchOption: SearchOption.TopDirectoryOnly).Length;

                var fileNameList = destination.IncludeSpecifications
                    .SelectMany(_ => Directory.GetFiles(sourceDirectoryName, _, SearchOption.TopDirectoryOnly)) 
                    .ToArray()
                    .OrderBy(_ => _)
                    .ToList();

                List<Regex> excludeRegexList = new();

                foreach (var excludeSpec in destination.ExcludeSpecifications)
                {
                    excludeRegexList.Add(excludeSpec.GenerateRegexForFileMask());
                }

                // Iterate backwards through the list so we can change it while iterating

                for (int i = fileNameList.Count - 1; i >= 0; i--)
                {
                    foreach (var excludeRegex in excludeRegexList.ToList())
                    {
                        if (excludeRegex.IsMatch(fileNameList[i]))
                        {
                            fileNameList.RemoveAt(i);
                            break;
                        }
                    }
                }

                result.AddDebug($"Checking {fileNameList.Count} files of {result.ItemsFound}");

                var stopwatch = Stopwatch.StartNew();

                foreach (var fileName in fileNameList)
                {
                    var fiSrc = new FileInfo(fileName);
                    string destinationFileName = Path.Combine(destination.DirectoryPath, fiSrc.Name);

                    var fiDest = new FileInfo(destinationFileName);

                    bool doCopy = true;

                    if (fiDest.Exists)
                    {
                        if (fiSrc.LastWriteTimeUtc == fiDest.LastWriteTimeUtc && fiSrc.Length == fiDest.Length)
                        {
                            doCopy = false;
                            //result.AddInfo("Source and destination have identical times and lengths, skipping");
                        }
                    }

                    if (doCopy)
                    {
                        string tempDestFileName = destinationFileName + ".copying";

                        result.AddInfo($"Copying {fileName} to {destinationFileName} {FileHelpers.GetByteSizeAsText(fiSrc.Length)}");

                        await _logService.ProcessResult(result);

                        try
                        {
                            if (File.Exists(tempDestFileName))
                            {
                                File.Delete(tempDestFileName);
                            }

                            using (FileStream SourceStream = File.Open(fileName, FileMode.Open))
                            {
                                using (FileStream DestinationStream = File.Create(tempDestFileName))
                                {
                                    await SourceStream.CopyToAsync(DestinationStream);
                                }
                            }

                            if (File.Exists(tempDestFileName))
                            {
                                File.Move(tempDestFileName, destinationFileName, true);
                            }

                            result.ItemsProcessed++;
                            result.BytesProcessed += fiSrc.Length;
                        }
                        catch (Exception fileException)
                        {
                            result.AddException(fileException);
                            await _logService.ProcessResult(result);
                        }

                        if (File.Exists(destinationFileName))
                        {
                            TotalBytesCopied += fiSrc.Length;
                            TotalFilesCopied++;

                            if (destination.SynchoniseFileTimestamps)
                            {
                                fiDest = new FileInfo(destinationFileName)
                                {
                                    LastWriteTimeUtc = fiSrc.LastWriteTimeUtc,
                                    CreationTimeUtc = fiSrc.CreationTimeUtc
                                };
                            }

                            if (destination.RetainVersions >= Constants.RETAIN_VERSIONS_MINIMUM)
                            {
                                result.SubsumeResult(
                                    await DeleteOldVersions(destinationFileName, destination.RetainVersions, destination.RetainDaysOld));
                            }
                        }
                        else
                        {
                            result.AddError($"Failed to copy to {destinationFileName}");
                        }
                    }
                }

                if (result.ItemsProcessed > 0)
                {
                    stopwatch.Stop();

                    double mbps = result.BytesProcessed / stopwatch.Elapsed.TotalSeconds / 1024 / 1024;

                    result.AddSuccess($"Copied {result.ItemsProcessed} files, total {FileHelpers.GetByteSizeAsText(result.BytesProcessed)} in {stopwatch.Elapsed.TotalSeconds:N0}s ({mbps:N0}MB/s)");

                    result.SubsumeResult(FileHelpers.CheckDiskSpace(destination.DirectoryPath));
                }
                else
                {
                    result.AddInfo($"No files needed copying");
                }
            }
            else
            {
                result.AddError($"Source directory does not exist");
            }

            await _logService.ProcessResult(result, reportItemCounts: true, addCompletionItem: true, itemNameSingular: "file");

            return result;
        }

        /// <summary>        
        /// We don't need the latest file, or more than one, just the first we find that is last 
        /// written after the specified time
        /// </summary>
        /// <param name="directoryName"></param>
        /// <param name="recursive"></param>
        /// <param name="laterThanUtc"></param>
        /// <returns></returns>
        internal static FileInfo GetLaterFile(string directoryName, bool recursive, DateTime laterThanUtc)
        {
            DirectoryInfo root = new(directoryName);

            // We're only looking for the first file we find with a later timestamp, let's
            // try the files in root first, even if we're recursive

            var rootFiles = root.GetFiles("*.*", SearchOption.TopDirectoryOnly);

            var later = rootFiles.FirstOrDefault(_ => _.LastWriteTimeUtc > laterThanUtc);

            if (later != null)
            {
                return later;
            }
            else if (recursive)
            {
                // Ah well, worth a try, check the subdirectories, if any, one by one. Best to do it
                // this way rather than GetFiles the whole lot, then start looking; we don't need the
                // full set, just one later file will do.

                foreach (var di in root.GetDirectories())
                {
                    var allFiles = di.GetFiles("*.*", SearchOption.AllDirectories); // Now we're recursive

                    later = allFiles.FirstOrDefault(_ => _.LastWriteTimeUtc > laterThanUtc);

                    if (later is not null)
                    {
                        return later;
                    }
                }
            }

            return null;
        }

        internal new void Dispose()
        {
            base.Dispose();
        }
    }
}
