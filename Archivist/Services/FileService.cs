﻿using Archivist.Classes;
using Archivist.Helpers;
using Archivist.Models;
using Archivist.Utilities;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Archivist.Services
{
    internal class FileService : BaseService
    {
        internal int TotalFilesCopied { get; private set; } = 0;
        internal long TotalBytesCopied { get; private set; } = 0;

        internal FileService(
            Job jobSpec,
            LogService logService) : base(jobSpec, logService)
        {
        }

        internal async Task<Result> DeleteTemporaryFiles(string directoryPath, bool zeroLengthOnly)
        {
            Result result = new("DeleteTemporaryFiles", functionQualifier: directoryPath);

            try
            {
                var temporaryFiles = Directory.GetFiles(directoryPath, "*.*", searchOption: SearchOption.TopDirectoryOnly)
                    .Where(_ => _.EndsWith(".copying") || _.EndsWith(".temporary"));

                foreach (var fileName in temporaryFiles)
                {
                    if (zeroLengthOnly == false || new FileInfo(fileName).Length == 0)
                    {
                        result.AddWarning($"Deleting old temporary file '{fileName}'");
                        File.Delete(fileName);
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

        internal async Task<Result> DeleteOldVersions(string latestFileName, int retainMinimumVersions, int retainMaximumVersions, int retainDaysOld)
        {
            Result result = new("DeleteOldVersions");

            try
            {
                // We shouldn't even be in here if RetainVersions isn't at least the minimum, but just in case...
                if (retainMinimumVersions < Constants.RETAIN_VERSIONS_MINIMUM)
                {
                    throw new Exception($"DeleteOldVersions invalid retainMinimumVersions {retainMinimumVersions} for '{latestFileName}'");
                }

                // Handy for debugging, a bit excessive otherwise
                string retainStr = $"DeleteOldVersions for '{latestFileName}' retaining min/max {retainMinimumVersions}/{retainMaximumVersions} versions and all files under {retainDaysOld} days old";

                result.AddDebug(retainStr);
                await _logService.ProcessResult(result);

                // The suffix is of the form -nnnn.zip, so for file abcde.zip we are looking for abcde-nnnnn.zip

                FileInfo fiArchive = new(latestFileName);

                string fileNamePrefix = fiArchive.Name[0..^9];
                string searchPattern = $"{fileNamePrefix}*.zip";

                var existingFiles = Directory.GetFiles(fiArchive.DirectoryName, searchPattern)
                    .Where(_ => _.Length == latestFileName.Length)
                    .OrderBy(_ => _);

                if (existingFiles.Any())
                {
                    if (existingFiles.Count() > retainMaximumVersions)
                    {
                        if (latestFileName == existingFiles.Last())
                        {
                            foreach (var fileName in existingFiles)
                            {
                                if (FileUtilities.IsFileVersioned(fileName, out string _) == false)
                                {
                                    result.AddError($"DeleteOldVersions found {fileName} not matching pattern");
                                }
                            }

                            if (result.HasNoErrorsOrWarnings)
                            {
                                // OK, it's safe, all the files look right, delete the excess

                                // They should already be ordered by ascending file name, but just in case...

                                var filesToDelete = existingFiles.OrderBy(_ => _).Take(existingFiles.Count() - retainMaximumVersions);

                                foreach (var fileName in filesToDelete)
                                {
                                    // Last second paranoia, whatever bug might get introduced above, never
                                    // delete the one we just created !

                                    if (fileName != latestFileName)
                                    {
                                        // Never delete anything that is younger than RetainDaysOld regardless of other settings

                                        if (FileUtilities.IsLastWrittenMoreThanDaysAgo(fileName, retainDaysOld, out DateTime lastWriteTimeUtc))
                                        {
                                            FileInfo fi = new(fileName);

                                            result.AddWarning($"Deleting file version '{fileName}' ({FileUtilities.GetByteSizeAsText(fi.Length)}, last write {lastWriteTimeUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} UTC)");
                                            File.Delete(fileName);

                                            result.Statistics.FilesDeleted++;
                                            result.Statistics.BytesDeleted += fi.Length;
                                        }
                                        else
                                        {
                                            result.AddDebug($"Retaining version '{fileName}', last written under {retainDaysOld} days ago ({lastWriteTimeUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} UTC)");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        result.AddDebug($"DeleteOldVersions for '{latestFileName}' found {existingFiles.Count()} version{existingFiles.Count().PluralSuffix()}, which is OK");
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
        /// Gneerate a report of all the files currently in all the various directories
        /// </summary>
        /// <param name="fileReport"></param>
        /// <returns></returns>
        internal async Task<Result> GenerateFileReport()
        {
            Result result = new("GenerateFileReport");

            FileReport fileReport = new();

            foreach (var fi in new DirectoryInfo(_jobSpec.PrimaryArchiveDirectoryName).GetFiles())
            {
                fileReport.Add(fi, true);
            }

            foreach (var dir in _jobSpec.ArchiveDirectories.Where(_ => _.IsAvailable))
            {
                foreach (var fi in new DirectoryInfo(dir.DirectoryPath).GetFiles())
                {
                    fileReport.Add(fi, false);
                }
            }

            result.AddInfo("File instance report;");

            foreach (var item in fileReport.Items.OrderBy(_ => _.Name))
            {
                var cnt = item.Instances.Count;
                string msg = $"File {item.Name} has {item.Instances.Count} {"instance".Pluralise(cnt)} {FileUtilities.GetByteSizeAsText(item.Length, true)}, last write {item.LastWriteUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} UTC";
                
                if (cnt >= 3)
                {
                    result.AddSuccess(msg);
                }
                else if (cnt >= 2)
                {
                    result.AddInfo(msg);
                }
                else
                {
                    result.AddWarning(msg);
                }

                foreach (var inst in item.Instances.OrderByDescending(_ => _.IsInPrimaryArchive).ThenBy(_ => _.Path))
                {
                    result.AddInfo($" {inst.Path} {FileUtilities.GetByteSizeAsText(inst.Length, true)}, last write {inst.LastWriteUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} UTC ({(inst.IsFuzzyMatch ? "Fuzzy" : "Exact")})");
                }
            }

            // Names and numbers table

            result.AddInfo("File version counts and date ranges;");

            int fnLen = fileReport.Items.Max(_ => _.Name.Length);

            result.AddInfo("File" + new string(' ', fnLen - 1) + "#  Size      Date & time");
            foreach (var item in fileReport.Items.OrderBy(_ => _.Name))
            {
                var minDate = item.Instances.Min(_ => _.LastWriteUtc);
                var size = FileUtilities.GetByteSizeAsText(item.Length);
                var cnt = item.Instances.Count;

                string msg = $"{item.Name} {new string(' ', fnLen - item.Name.Length)} {cnt, 2}  {size}{new string(' ', 9-size.Length)} {minDate.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS_FIXED_WIDTH)}";

                if (cnt >= 3)
                {
                    result.AddSuccess(msg);
                }
                else if (cnt >= 2)
                {
                    result.AddInfo(msg);
                }
                else
                {
                    result.AddWarning(msg);
                }
            }

            // Duplicates report

            var duplicateNames = fileReport.Items.GroupBy(_ => _.Name).Where(g => g.Count() > 1).Select(y => y).ToList();

            if (duplicateNames.Any())
            {
                result.AddWarning("Files with same name but significantly different sizes or dates;");
                result.AddInfo("Disks formatted with different file syttems or allocation sizes will report different sizes for the same file");

                foreach (var dup in duplicateNames.OrderBy(_ => _.Key))
                {
                    result.AddWarning($"{dup.Key}");
                    
                    foreach(var df in fileReport.Items.Where(_ => _.Name == dup.Key).OrderBy(_ => _.LastWriteUtc))
                    {
                        result.AddWarning($" {df.Instances.Count} {"instance".Pluralise(df.Instances.Count)} {FileUtilities.GetByteSizeAsText(df.Length, true)}, last write {df.LastWriteUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} UTC");

                        foreach (var inst in df.Instances.OrderByDescending(_ => _.IsInPrimaryArchive).ThenByDescending(_ => _.IsFuzzyMatch).ThenBy(_ => _.Path))
                        {
                            result.AddInfo($"  {inst.Path} {FileUtilities.GetByteSizeAsText(inst.Length, true)}, last write {inst.LastWriteUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} UTC ({(inst.IsFuzzyMatch ? "Fuzzy" : "Exact")})");
                        }
                    }
                }
            }

            // Combine files with same root and check for any with too few/too old copies

            //var rootNames = fileReport.Items.Select(_ => _.RootName).Distinct();

            //fnLen = rootNames.Max(_ => _.Length);

            //result.AddInfo("File version counts and date ranges by root name;");
            //result.AddInfo("Root file" + new string(' ', fnLen - 6) + "#  Most recent version");

            //foreach (var rn in rootNames.OrderBy(_ => _))
            //{
            //    var instances = fileReport.Items.Where(_ => _.RootName == rn).SelectMany(_ => _.Instances);

            //    var cnt = instances.Count();
            //    var mostRecentDate = instances.Min(_ => _.LastWriteUtc);
            //    var msg = $"{rn} {new string(' ', fnLen - rn.Length)} {cnt,2}  {mostRecentDate.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS_FIXED_WIDTH)}";

            //    if (cnt >= 3)
            //    {
            //        result.AddSuccess(msg);
            //    }
            //    else if (cnt >= 2)
            //    {
            //        result.AddInfo(msg);
            //    }
            //    else
            //    {
            //        result.AddWarning(msg);
            //    }
            //}

            await _logService.ProcessResult(result);

            return result;
        }

        internal async Task<Result> CopyToArchives()
        {
            Result result = new("CopyToArchives", true);

            foreach (var destination in _jobSpec.ArchiveDirectories
                .Where(_ => _.IsToBeProcessed(_jobSpec))
                .OrderBy(_ => _.Priority)
                .ThenBy(_ => _.DirectoryPath))
            {
                result.SubsumeResult(await DeleteTemporaryFiles(destination.DirectoryPath, false));

                Result copyResult = await CopyArchives(_jobSpec.PrimaryArchiveDirectoryName, destination);

                result.SubsumeResult(copyResult);

                await _logService.ProcessResult(result);

                if (result.HasErrors)
                    break;
            }

            await _logService.ProcessResult(result, reportCompletion: true);

            await _logService.ProcessResult(result);

            return result;
        }

        /// <summary>
        /// Copies from one directory to another, NOT RECURSIVE
        /// </summary>
        /// <param name="sourceDirectoryName"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        internal async Task<Result> CopyArchives(string sourceDirectoryName, ArchiveDirectory destination)
        {
            string destName = string.IsNullOrEmpty(destination.VolumeLabel)
                ? $"'{destination.DirectoryPath}'"
                : $"volume '{destination.VolumeLabel}', path '{destination.DirectoryPath}'";

            Result result = new(
                functionName: "CopyArchives",
                addStartingItem: true,
                functionQualifier: $"from '{sourceDirectoryName}' to {destName}");

            result.AddInfo($"Including {destination.IncludeSpecificationsText}, excluding { destination.ExcludeSpecificationsText}");

            result.SubsumeResult(FileUtilities.CheckDiskSpace(destination.DirectoryPath, destination.VolumeLabel));

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

                result.Statistics.ItemsFound = Directory.GetFiles(sourceDirectoryName, searchPattern: "*.*", searchOption: SearchOption.TopDirectoryOnly).Length;

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
                        // If we specifically exclude this file name

                        if (excludeRegex.IsMatch(fileNameList[i]))
                        {
                            fileNameList.RemoveAt(i);
                            break;
                        }
                    }
                }

                result.AddDebug($"Checking {fileNameList.Count} files of {result.Statistics.ItemsFound}");

                // OK, we have a bit of a code smell coming up, we want to detect where files will be copied over from the 
                // source directory to the archive directory then immediately be deleted because the RetainVersions setting for
                // the source is larger than that for the destiation, assuming the RetainDaysOld setting allows it.

                // Because we are processing the folder as a whole, we're not looking at them in terms of a bunch of versioned
                // sets, but as a list of file names, which makes things awkward.

                // Clearly I didn't take this into account when I wrote it but rather than refactor this whole section, we
                // are going to filter the list of files to copy by scanning through them, deconstructing the names to work
                // out which are versioned sets and removing the ones that will subsequently be deleted anyway.

                // Not too bad a code smell, but a bit whiffy for sure.

                // When refactoring this, just build a list of what files should end up in the destination, then add/delete
                // files to/from it according to that, adding first so as not to end up with no files if we crash out or get
                // abandoned - which requires more space of course but is the safest approach.

                fileNameList = RemoveFilesThatWouldJustGetDeletedAnyway(fileNameList, destination.RetainMinimumVersions, destination.RetainMaximumVersions, destination.RetainYoungerThanDays);

                // Now that slightly embarrassing process is done, we're ready to copy the list of files over

                var stopwatch = Stopwatch.StartNew();

                foreach (var fileName in fileNameList.OrderBy(_ => _))
                {
                    var fiSrc = new FileInfo(fileName);
                    string destinationFileName = Path.Combine(destination.DirectoryPath, fiSrc.Name);

                    var fiDest = new FileInfo(destinationFileName);

                    bool doCopy = true;

                    result.AddDebug($"Processing source {fileName}, destination {destinationFileName}");

                    if (fiDest.Exists)
                    {
                        if (fiSrc.LastWriteTimeUtc.CompareTo(fiDest.LastWriteTimeUtc) == 0)
                        {
                            doCopy = false;
                            result.AddDebug($"Source and destination for '{fiSrc.Name}' have identical last write times, skipping ({fiSrc.LastWriteTimeUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)})");
                        }
                        else
                        {
                            var howStale = fiSrc.LastWriteTimeUtc - fiDest.LastWriteTimeUtc;

                            if (howStale.TotalMinutes < 5)
                            {
                                doCopy = false;
                                result.AddDebug($"Source and destination for '{fiSrc.Name}' have close enough write times, skipping ({fiSrc.LastWriteTimeUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} and {fiDest.LastWriteTimeUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)})");
                            }
                        }

                        if (doCopy)
                        {
                            result.AddDebug($"Source and destination for '{fiSrc.Name}' differ, dates {fiSrc.LastWriteTimeUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} and {fiDest.LastWriteTimeUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} (lengths {fiSrc.Length:N0} / {fiDest.Length:N0})");
                        }
                    }
                    else
                    {
                        result.AddDebug($"Destination '{destinationFileName}' does not exist");
                    }

                    if (doCopy)
                    {
                        double spaceAvailable = FileUtilities.GetAvailableDiskSpace(destination.DirectoryPath);

                        if (spaceAvailable < fiSrc.Length)
                        {
                            doCopy = false;
                            result.AddWarning($"Insufficient space to copy {fiSrc.Name} to {destination.DirectoryPath}");
                        }
                    }

                    if (doCopy)
                    {
                        string tempDestFileName = destinationFileName + ".copying";

                        // Don't write this to the console, it gets it's own snazzy progress indicator
                        result.AddDebug($"Copying {fileName} to {destinationFileName} {FileUtilities.GetByteSizeAsText(fiSrc.Length)}");
                        await _logService.ProcessResult(result);

                        try
                        {
                            if (File.Exists(tempDestFileName))
                            {
                                File.Delete(tempDestFileName);
                            }

                            decimal percentageComplete = 0;

                            var progressReporter = new Progress<KeyValuePair<long, long>>();

                            LogEntry progressLogEntry = new(
                                percentComplete: 0,
                                prefix: $"Copying {fiSrc.Name}", // {fileName} to {destination.DirectoryPath}",
                                suffix: $"of {FileUtilities.GetByteSizeAsText(fiSrc.Length)}" // complete"
                            );

                            progressReporter.ProgressChanged += delegate (object obj, KeyValuePair<long, long> progressValue)
                            {
                                if (progressValue.Key == 0)
                                {
                                    progressLogEntry.PercentComplete = 0;
                                    _logService.LogToConsole(progressLogEntry);
                                }
                                else if (progressValue.Key == progressValue.Value)
                                {
                                    progressLogEntry.PercentComplete = 100;
                                    _logService.LogToConsole(progressLogEntry);
                                }
                                else
                                {
                                    decimal thisPercentage = ((decimal)progressValue.Key / (decimal)progressValue.Value) * 100;

                                    if (thisPercentage > (percentageComplete + 1))
                                    {
                                        percentageComplete = thisPercentage;
                                        progressLogEntry.PercentComplete = (short)percentageComplete;
                                        _logService.LogToConsole(progressLogEntry);
                                    }
                                }
                            };

                            using (FileStream sourceStream = File.Open(fileName, FileMode.Open))
                            {
                                using (FileStream destinationStream = File.Create(tempDestFileName))
                                {
                                    await sourceStream.CopyToAsyncProgress(sourceStream.Length, destinationStream, progressReporter, default);
                                }
                            }

                            if (File.Exists(tempDestFileName))
                            {
                                result.AddInfo($"Copied {fiSrc.Name} to {destination.DirectoryPath} ({FileUtilities.GetByteSizeAsText(fiSrc.Length)}) OK");
                                await _logService.ProcessResult(result);
                                File.Move(tempDestFileName, destinationFileName, true);
                            }

                            result.Statistics.ItemsProcessed++;
                            result.Statistics.BytesProcessed += fiSrc.Length;

                            result.Statistics.FilesAdded += 1;
                            result.Statistics.BytesAdded += fiSrc.Length;

                            destination.Statistics.FilesAdded += 1;
                            destination.Statistics.BytesAdded += fiSrc.Length;
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

                            if (destination.RetainMinimumVersions >= Constants.RETAIN_VERSIONS_MINIMUM)
                            {
                                result.SubsumeResult(
                                    await DeleteOldVersions(destinationFileName, destination.RetainMinimumVersions, destination.RetainMaximumVersions, destination.RetainYoungerThanDays));
                            }
                        }
                        else
                        {
                            result.AddError($"Failed to copy to {destinationFileName}");
                        }
                    }
                }

                if (result.Statistics.ItemsProcessed > 0)
                {
                    stopwatch.Stop();

                    double mbps = result.Statistics.BytesProcessed / stopwatch.Elapsed.TotalSeconds / 1024 / 1024;

                    result.AddSuccess($"Copied {result.Statistics.ItemsProcessed} files from '{sourceDirectoryName}' to {destName}, total {FileUtilities.GetByteSizeAsText(result.Statistics.BytesProcessed)} in {stopwatch.Elapsed.TotalSeconds:N0}s ({mbps:N0}MB/s)");

                    result.SubsumeResult(FileUtilities.CheckDiskSpace(destination.DirectoryPath, destination.VolumeLabel));
                }
                else
                {
                    result.AddInfo($"No files needed copying from '{sourceDirectoryName}' to {destName}");
                }
            }
            else
            {
                result.AddError($"Source directory does not exist");
            }

            await _logService.ProcessResult(result, reportItemCounts: true, reportCompletion: true, reportAllStatistics: true);

            return result;
        }

        /// <summary>
        /// This is a cure for the bad bit of design where files get copied to archive, then are immediately deleted due 
        /// to the RetainVersions being larger on the source than the destination. This works just fine but should 
        /// be refactored out at some point, see comment block in CopyArchives.        /// </summary>
        /// <param name="fileNameList"></param>
        /// <param name="retainMinimumVersions"></param>
        /// <param name="retainMaximumVersions"></param>
        /// <param name="retainDaysOld"></param>
        /// <returns></returns>
        private List<string> RemoveFilesThatWouldJustGetDeletedAnyway(List<string> fileNameList, int retainMinimumVersions, int retainMaximumVersions, int retainDaysOld)
        {
            // Named sets of file name lists, one for each base file name
            Dictionary<string, List<string>> versionedFileSets = new();

            // What we will hand back to the caller
            List<string> filesToProcess = new();

            foreach (var fileName in fileNameList.OrderBy(_ => _))
            {
                if (FileUtilities.IsFileVersioned(fileName, out string baseFileName))
                {
                    var idxFileStart = fileName.LastIndexOf(Path.DirectorySeparatorChar) + 1; // Where the file name starts
                    var idxHyphen = fileName.LastIndexOf("-"); // Where the - of -nnn is
                    //var baseFileName = fileName[idxFileStart..idxHyphen];

                    if (versionedFileSets.ContainsKey(baseFileName))
                    {
                        var existingFileSet = versionedFileSets[baseFileName];
                        existingFileSet.Add(fileName);
                    }
                    else
                    {
                        versionedFileSets.Add(baseFileName, new List<string> { fileName });
                    }
                }
                else
                {
                    // Non-versioned file, we always copy these
                    filesToProcess.Add(fileName);
                }
            }

            // Each versioned file set now has a list of files in alpha order of name, so
            // oldest generation first, newest last.

            foreach (var fileSet in versionedFileSets)
            {
                int idx = 0;
                int keepVersionFromIdx = fileSet.Value.Count - retainMaximumVersions;

                foreach (var takeFileName in fileSet.Value.OrderByDescending(_ => _))
                {
                    idx++;

                    // Regardless of other criteria, always retain files under X days old

                    if (idx >= keepVersionFromIdx || FileUtilities.IsLastWrittenLessThanDaysAgo(takeFileName, retainDaysOld, out _))
                    {
                        filesToProcess.Add(takeFileName);
                    }
                }
            }

            return filesToProcess;
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
                // Ah well, worth a try, check any subdirectories one by one. Best to do it
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
