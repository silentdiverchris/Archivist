using Archivist.Classes;
using Archivist.Helpers;
using Archivist.Models;
using Archivist.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Archivist.Enumerations;

namespace Archivist.Services
{
    internal class FileService : BaseService
    {
        internal int TotalFilesCopied { get; private set; } = 0;
        internal long TotalBytesCopied { get; private set; } = 0;

        internal FileService(
            Job jobSpec,
            AppSettings appSettings,
            LogService logService) : base(jobSpec, appSettings, logService)
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

        internal async Task<Result> DeleteOldVersions(
            string directoryPath,
            string baseFileName,
            int retainMinimumVersions,
            int retainMaximumVersions,
            int retainYoungerThanDays)
        {
            Result result = new("DeleteOldVersions");

            try
            {
                // The below should get caught on startup, but just in case...

                if (retainMinimumVersions < Constants.RETAIN_VERSIONS_MINIMUM)
                {
                    throw new Exception($"DeleteOldVersions invalid retainMinimumVersions {retainMinimumVersions} for '{directoryPath}'");
                }

                if (retainYoungerThanDays < Constants.RETAIN_DAYS_OLD_MINIMUM)
                {
                    throw new Exception($"DeleteOldVersions invalid retainYoungerThanDays {retainYoungerThanDays} for '{directoryPath}'");
                }

                // Handy for debugging, a bit excessive otherwise
                //string retainStr = $"DeleteOldVersions for '{baseFileName}' in '{directoryPath}' retaining min/max {retainMinimumVersions}/{retainMaximumVersions} versions and all files under {retainYoungerThanDays} day{retainYoungerThanDays.PluralSuffix()} old";
                //result.AddInfo(retainStr);
                //await _logService.ProcessResult(result);

                var existingFiles = directoryPath.GetVersionedFiles(baseFileName);

                if (existingFiles.Any())
                {
                    if (existingFiles.Count >= retainMaximumVersions)
                    {
                        // They should already be ordered by ascending file name, but just in case...

                        var filesToDelete = existingFiles.OrderBy(_ => _).Take(existingFiles.Count - retainMaximumVersions).ToList();

                        if (filesToDelete.Any())
                        {
                            // Sanity checks / abundance of caution, the above code should be making the right
                            // decisions but check again in case a bug is introduced above

                            if (filesToDelete.Count < existingFiles.Count)
                            {
                                int numberToRetain = existingFiles.Count - filesToDelete.Count;

                                if (numberToRetain >= retainMinimumVersions && numberToRetain >= Constants.RETAIN_VERSIONS_MINIMUM)
                                {
                                    foreach (var fileName in filesToDelete)
                                    {
                                        // Never delete anything that is younger than retainYoungerThanDays regardless of other settings

                                        if (FileUtilities.IsYoungerThanDays(fileName, retainYoungerThanDays, out DateTime lastWriteTimeLocal, out long fileLength))
                                        {
                                            result.AddWarning($"Deleting file version '{fileName}' ({FileUtilities.GetByteSizeAsText(fileLength)}, older than {retainYoungerThanDays} days, last write {lastWriteTimeLocal.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} local)");
                                            File.Delete(fileName);
                                        }
                                        //else
                                        //{
                                        //    result.AddInfo($"Retaining version '{fileName}', last written under {retainYoungerThanDays} days ago ({lastWriteTimeLocal.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} UTC)");
                                        //}
                                    }
                                }
                                else
                                {
                                    result.AddError($"DeleteOldVersions somehow decided to leave fewer than retainMinimumVersions ({numberToRetain}/{retainMinimumVersions}/{Constants.RETAIN_VERSIONS_MINIMUM}), not deleting anything");
                                }
                            }
                            else
                            {
                                result.AddError($"DeleteOldVersions somehow decided that all {filesToDelete.Count} versions should be deleted, not deleting anything");
                            }
                        }
                    }
                    else
                    {
                        result.AddDebug($"DeleteOldVersions found {existingFiles.Count} version{existingFiles.Count.PluralSuffix()}, which is OK");
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
        /// Generate a report of all the files currently in all the various directories
        /// </summary>
        /// <param name="fileReport"></param>
        /// <returns></returns>
        internal async Task<Result> GenerateFileReport()
        {
            Result result = new("GenerateFileReport");

            FileReport fileReport = new();

            foreach (var fi in new DirectoryInfo(_jobSpec.PrimaryArchiveDirectoryPath!).GetFiles())
            {
                fileReport.Add(fi, true);
            }

            foreach (var dir in _jobSpec.ArchiveDirectories.Where(_ => _.IsAvailable))
            {
                foreach (var fi in new DirectoryInfo(dir.DirectoryPath!).GetFiles())
                {
                    fileReport.Add(fi, false);
                }
            }

            result.AddInfo("File instance report;", consoleBlankLineBefore: true);

            foreach (var item in fileReport.Items.OrderBy(_ => _.FileName))
            {
                var cnt = item.Instances.Count;
                string msg = $"{item.FileName} has {item.Instances.Count} {"instance".Pluralise(cnt)} {FileUtilities.GetByteSizeAsText(item.Length, false)}, last write {item.LastWriteUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} UTC";

                if (cnt >= 3)
                {
                    result.AddSuccess(msg, consoleBlankLineBefore: true);
                }
                else if (cnt >= 2)
                {
                    result.AddInfo(msg, consoleBlankLineBefore: true);
                }
                else
                {
                    result.AddWarning(msg, consoleBlankLineBefore: true);
                }

                foreach (var inst in item.Instances.OrderByDescending(_ => _.IsInPrimaryArchive).ThenBy(_ => _.Path))
                {
                    result.AddInfo($" {inst.Path} {FileUtilities.GetByteSizeAsText(inst.Length, false)}, last write {inst.LastWriteUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} UTC ({(inst.IsFuzzyMatch ? "Fuzzy" : "Exact")})");
                }
            }

            // Names and numbers table

            result.AddInfo("File version counts and date ranges;", consoleBlankLineBefore: true);

            int fnLen = fileReport.Items.Max(_ => _.FileName.Length);

            result.AddInfo("File" + new string(' ', fnLen - 1) + "#  Size      Date & time");
            foreach (var item in fileReport.Items.OrderBy(_ => _.FileName))
            {
                var minDate = item.Instances.Min(_ => _.LastWriteUtc);
                var size = FileUtilities.GetByteSizeAsText(item.Length);
                var cnt = item.Instances.Count;

                string msg = $"{item.FileName} {new string(' ', fnLen - item.FileName.Length)} {cnt,2}  {size}{new string(' ', 9 - size.Length)} {minDate.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS_FIXED_WIDTH)}";

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

            var duplicateNames = fileReport.Items.GroupBy(_ => _.FileName).Where(g => g.Count() > 1).Select(y => y).ToList();

            if (duplicateNames.Any())
            {
                result.AddWarning("Files with same name but significantly different sizes or dates;", consoleBlankLineBefore: true);

                //result.AddInfo("Disks formatted with different file systems or allocation sizes will report different sizes for the same file");

                foreach (var dup in duplicateNames.OrderBy(_ => _.Key))
                {
                    result.AddWarning($"{dup.Key}", consoleBlankLineBefore: true);

                    foreach (var df in fileReport.Items.Where(_ => _.FileName == dup.Key).OrderBy(_ => _.LastWriteUtc))
                    {
                        result.AddWarning($" {df.Instances.Count} {"instance".Pluralise(df.Instances.Count)} {FileUtilities.GetByteSizeAsText(df.Length, true)}, last write {df.LastWriteUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} UTC");

                        foreach (var inst in df.Instances.OrderByDescending(_ => _.IsInPrimaryArchive).ThenByDescending(_ => _.IsFuzzyMatch).ThenBy(_ => _.Path))
                        {
                            result.AddInfo($"  {inst.Path} {FileUtilities.GetByteSizeAsText(inst.Length, true)}, last write {inst.LastWriteUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} UTC ({(inst.IsFuzzyMatch ? "Fuzzy" : "Exact")})");
                        }
                    }
                }
            }

            // Find files with a scarily low number of copies, or which are worryingly stale

            int concerningFiles = 0;
            var rootNames = fileReport.Items.Select(_ => _.BaseFileName).Distinct().OrderBy(_ => _);

            foreach (var rootFileName in rootNames)
            {
                List<string> Concerns = new();

                var thresholdHours = 12;
                var recentThresholdLocal = DateTime.Now.AddHours(-thresholdHours);
                var instances = fileReport.Items.Where(_ => _.BaseFileName == rootFileName).SelectMany(_ => _.Instances).OrderBy(_ => _.Path);
                var latestInstance = instances.OrderByDescending(_ => _.LastWriteUtc).FirstOrDefault();
                var latestSize = FileUtilities.GetByteSizeAsText(latestInstance!.Length);
                var copyCount = instances.Count();
                var sourceDirectory = FindSourceDirectory(rootFileName);

                if (copyCount < 2)
                {
                    var whereFiles = instances.Select(_ => _.Path);
                    var msg = copyCount == 1
                        ? $"Only 1 version of this archive exists, in {string.Join(", ", whereFiles)}"
                        : $"Only {copyCount} versions of this archive exist, in {string.Join(", ", whereFiles)}";

                    Concerns.Add(msg);
                }

                if (sourceDirectory is not null && latestInstance.LastWriteLocal < recentThresholdLocal)
                {
                    var laterFile = GetLaterFile(sourceDirectory.DirectoryPath!, true, latestInstance.LastWriteUtc);

                    if (laterFile is not null)
                    {
                        // Show in local time
                        var hoursOld = (int)DateTime.Now.Subtract(latestInstance.LastWriteLocal).TotalHours;

                        var msg = $"Latest archive {latestInstance.FileName} is ";

                        msg += hoursOld == 0
                            ? $"less than an hour"
                            : hoursOld == 1
                                ? $"one hour"
                                : $"{hoursOld} hours";

                        msg += $" old and there are changes since then (eg. {laterFile.Name} {laterFile.LastWriteTime.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)})";

                        Concerns.Add(msg);
                    }
                }

                if (Concerns.Any())
                {
                    concerningFiles++;

                    string name = sourceDirectory?.DirectoryPath ?? rootFileName;
                    result.AddInfo(name, consoleBlankLineBefore: true);

                    foreach (var concern in Concerns)
                    {
                        result.AddWarning(concern);
                    }
                }
            }

            var unmountedButAvailables = _jobSpec.ArchiveDirectories.Where(_ => _.IsEnabled && _.IsAvailable == false);

            foreach (var uba in unmountedButAvailables)
            {
                var volLabel = string.IsNullOrEmpty(uba.VolumeLabel)
                    ? null
                    : $"'{uba.VolumeLabel}' ";

                var dirPath = uba.DirectoryPath!.Contains(Path.DirectorySeparatorChar)
                    ? $"'{uba.DirectoryPath}' "
                    : null;

                if (uba.IsRemovable)
                {
                    result.AddInfo($"Removable archive destination {volLabel}{dirPath}is enabled but not available.", consoleBlankLineBefore: uba == unmountedButAvailables.First(), consoleBlankLineAfter: uba == unmountedButAvailables.Last());
                }
                else
                {
                    result.AddWarning($"Fixed archive destination {volLabel}{dirPath}is enabled but not available.", consoleBlankLineBefore: uba == unmountedButAvailables.First(), consoleBlankLineAfter: uba == unmountedButAvailables.Last());
                }
            }

            await _logService.ProcessResult(result);

            return result;
        }

        /// <summary>
        /// Dig around to try to discover which source directory resulted in this archive file, we'll removve 
        /// the need for this slightly embarrassing functionality when the new ArchiveRegister functionality 
        /// implements the compression actions.
        /// </summary>
        /// <param name="baseFileName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private SourceDirectory? FindSourceDirectory(string baseFileName)
        {
            try
            {
                var matches = _jobSpec.SourceDirectories.Where(_ => _.IsEnabled && _.OverrideOutputFileName == baseFileName);

                if (matches.Any() == false)
                {
                    matches = _jobSpec.SourceDirectories.Where(_ => _.IsEnabled && _.DirectoryPath!.EndsWith(baseFileName));
                }

                if (matches.Any())
                {
                    if (matches.Count() == 1)
                    {
                        return matches.First();
                    }
                    else
                    {
                        // Two or more source directories have the same lowest level folder name, we
                        // need to look a little deeper... 

                        foreach (var match in matches)
                        {
                            var fn = FileUtilities.GenerateBaseOutputFileName(match);

                            if (fn.StartsWith(baseFileName))
                            {
                                return match;
                            }
                        }

                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"FindSourceDirectory for {baseFileName}", ex);
            }
        }

        internal async Task<Result> ExecuteFileDeleteActions(ArchiveRegister archiveRegister)
        {
            Result result = new("ExecuteFileDeleteActions", false);

            var filesToDelete = archiveRegister.Actions
                .Where(_ => _.Type == enArchiveActionType.DeleteFromPrimary || _.Type == enArchiveActionType.DeleteFromDestination)
                .OrderBy(_ => _.SourceFile!.FullName)
                .Select(_ => _.SourceFile);

            foreach (var file in filesToDelete)
            {
                result.AddWarning($"Deleting file '{file!.FullName}' ({FileUtilities.GetByteSizeAsText(file.Length)}");
                File.Delete(file.FullName);
            }

            await _logService.ProcessResult(result);

            return result;
        }

        internal async Task<Result> ExecuteFileCopyActions(ArchiveRegister archiveRegister)
        {
            Result result = new("ExecuteFileCopyActions", false);

            await DeleteTemporaryFiles();

            // Copy the latest, priority and smallest files first. If destination disk space is limited, best to fit
            // several of the latest, high priority and smallest archives in than fill the lot with one large archive

            var actions = archiveRegister.Actions
                .Where(_ => _.Type == enArchiveActionType.CopyToDestination)
                .OrderByDescending(_ => _.SourceFile!.IslatestVersion)
                .OrderBy(_ => _.SourceFile!.SourcePriority)
                .OrderBy(_ => _.SourceFile!.Length)
                .ThenBy(_ => _.DestinationDirectory!.Path);

            foreach (var act in actions)
            {
                double spaceAvailable = FileUtilities.GetAvailableDiskSpace(act.DestinationDirectory!.Path);

                if (spaceAvailable < act.SourceFile!.Length)
                {
                    result.AddWarning($"Insufficient space to copy {act.SourceFile.FullName} to {act.DestinationDirectory.Path}");
                }
                else
                {
                    Result copyResult = await CopyFile(act.SourceFile, act.DestinationDirectory);
                    result.SubsumeResult(copyResult);
                }
            }

            await _logService.ProcessResult(result);

            return result;
        }

        internal async Task<Result> DeleteTemporaryFiles()
        {
            Result result = new("DeleteTemporaryFiles", false);

            foreach (var destination in _jobSpec.ArchiveDirectories
                .Where(_ => _.IsToBeProcessed(_jobSpec))
                .OrderBy(_ => _.Priority)
                .ThenBy(_ => _.DirectoryPath))
            {
                result.SubsumeResult(await DeleteTemporaryFiles(destination.DirectoryPath!, false));

                await _logService.ProcessResult(result);

                if (result.HasErrors)
                    break;
            }

            await _logService.ProcessResult(result);

            return result;
        }

        internal async Task<Result> CopyFile(ArchiveFileInstance srcFil, ArchiveDestinationDirectory dstDir)
        {
            Result result = new($"CopyFile {srcFil.FullName} to {dstDir.Path}");

            string dstFullName = Path.Combine(dstDir.Path, srcFil.FileName);

            //if (Constants.JUST_TESTING)
            //{
            //    result.AddInfo($"Pretending to copy {srcFil.FullName} to {dstFullName}{(srcFil.IslatestVersion ? " (latest)" : null)}");
            //    return result;
            //}

            try
            {
                string tempDestFileName = dstFullName + ".copying";

                // Don't write this to the console, it gets it's own snazzy progress indicator
                result.AddDebug($"Copying {srcFil.FullName} to {dstDir.Path} {FileUtilities.GetByteSizeAsText(srcFil.Length)}");
                await _logService.ProcessResult(result);

                if (File.Exists(tempDestFileName))
                {
                    File.Delete(tempDestFileName);
                }

                decimal percentageComplete = 0;

                Progress<KeyValuePair<long, long>> progressReporter = new();

                LogEntry progressLogEntry = new(
                    percentComplete: 0,
                    prefix: $"Copying {srcFil.FullName}",
                    suffix: $"of {FileUtilities.GetByteSizeAsText(srcFil.Length)}"
                );

                progressReporter.ProgressChanged += delegate (object? obj, KeyValuePair<long, long> progressValue)
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

                using (FileStream sourceStream = File.Open(srcFil.FullName, FileMode.Open))
                {
                    using (FileStream destinationStream = File.Create(tempDestFileName))
                    {
                        await sourceStream.CopyToAsyncProgress(sourceStream.Length, destinationStream, progressReporter, default);
                    }
                }

                if (File.Exists(tempDestFileName))
                {
                    result.AddSuccess($"Copied {srcFil.FullName} to {dstDir.Path} ({FileUtilities.GetByteSizeAsText(srcFil.Length)}) OK");
                    await _logService.ProcessResult(result);
                    File.Move(tempDestFileName, dstFullName, true);
                }

                result.Statistics.FiledAdded(srcFil.Length);

                if (File.Exists(dstFullName))
                {
                    TotalBytesCopied += srcFil.Length;
                    TotalFilesCopied++;

                    var fiDest = new FileInfo(dstFullName)
                    {
                        LastWriteTimeUtc = srcFil.LastWriteTimeUtc,
                        CreationTimeUtc = srcFil.CreationTimeUtc
                    };
                }
                else
                {
                    result.AddError($"Failed to copy to {dstFullName}");
                }
            }
            catch (Exception ex)
            {
                result.AddException(ex);
                await _logService.ProcessResult(result);
            }

            return result;
        }

        /// <summary>
        /// This is a temporary cure for the bug where files get copied to archive, then are immediately deleted due 
        /// to the RetainVersions being larger on the source than the destination. This works just fine but should 
        /// be refactored out at some point.      
        /// </summary>
        /// <param name="fileNameList"></param>
        /// <param name="retainMinimumVersions"></param>
        /// <param name="retainMaximumVersions"></param>
        /// <param name="retainYoungerThanDays"></param>
        /// <returns></returns>
        private List<string> GenerateVersionedFileSets(List<string> fileNameList, int retainMinimumVersions, int retainMaximumVersions, int retainYoungerThanDays, out Dictionary<string, List<string>> versionedFileSets)
        {
            // Named sets of file name lists, one for each base file name
            versionedFileSets = new();

            // What we will hand back to the caller
            List<string> filesToProcess = new();

            foreach (var fileName in fileNameList.OrderBy(_ => _))
            {
                if (fileName.IsVersionedFileName())
                {
                    var baseFileName = FileVersionHelpers.GetBaseFileName(fileName);

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
                    // Non-versioned file, not created by Archivist, we always copy these
                    filesToProcess.Add(fileName);
                }
            }

            // Each versioned file set now has a list of files in alpha order of name, so
            // oldest generation first, newest last.

            foreach (var fileSet in versionedFileSets)
            {
                int idx = 1;
                int copyTheFirstX = fileSet.Value.Count - retainMaximumVersions;

                foreach (var takeFileName in fileSet.Value.OrderByDescending(_ => _))
                {
                    // Regardless of other criteria, always copy files under X days old

                    if (idx <= copyTheFirstX || FileUtilities.IsYoungerThanDays(takeFileName, retainYoungerThanDays, out _, out _))
                    {
                        filesToProcess.Add(takeFileName);
                    }

                    idx++;
                }
            }

            return filesToProcess;
        }

        /// <summary>        
        /// We don't need the latest file, or more than one, just the first we find that is last 
        /// written after the specified time, returns null if no later files exist
        /// </summary>
        /// <param name="directoryName"></param>
        /// <param name="recursive"></param>
        /// <param name="laterThanUtc"></param>
        /// <returns></returns>
        internal static FileInfo? GetLaterFile(string directoryName, bool recursive, DateTime laterThanUtc)
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
