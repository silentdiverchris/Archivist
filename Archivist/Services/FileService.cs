using Archivist.Classes;
using Archivist.Helpers;
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

        internal async Task<Result> DeleteTemporaryFilesInDirectory(string directoryPath, bool zeroLengthOnly)
        {
            Result result = new("DeleteTemporaryFilesInDirectory", functionQualifier: directoryPath);

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
                string msg = $"{item.FileName} has {item.Instances.Count} {"instance".Pluralise(cnt)} {FileUtilities.GetByteSizeAsText(item.Length, false)}, last write {item.LastWriteTimeLocal.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)}";

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
                    result.AddInfo($" {inst.Path} {FileUtilities.GetByteSizeAsText(inst.Length, false)}, last write {inst.LastWriteLocal.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)}");
                }
            }

            // Names and numbers table

            result.AddInfo("File version counts and date ranges;", consoleBlankLineBefore: true);

            int fnLen = fileReport.Items.Max(_ => _.FileName.Length);

            result.AddInfo("File" + new string(' ', fnLen - 1) + "#  Size      Date & time");
            foreach (var item in fileReport.Items.OrderBy(_ => _.FileName))
            {
                var minDate = item.Instances.Min(_ => _.LastWriteLocal);
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

                    foreach (var df in fileReport.Items.Where(_ => _.FileName == dup.Key).OrderBy(_ => _.LastWriteTimeLocal))
                    {
                        result.AddWarning($" {df.Instances.Count} {"instance".Pluralise(df.Instances.Count)} {FileUtilities.GetByteSizeAsText(df.Length, true)}, last write {df.LastWriteTimeLocal.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)}");

                        foreach (var inst in df.Instances.OrderByDescending(_ => _.IsInPrimaryArchive).ThenByDescending(_ => _.IsFuzzyMatch).ThenBy(_ => _.Path))
                        {
                            result.AddInfo($"  {inst.Path} {FileUtilities.GetByteSizeAsText(inst.Length, true)}, last write {inst.LastWriteLocal.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)}");
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
                var latestInstance = instances.OrderByDescending(_ => _.LastWriteLocal).FirstOrDefault();
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
                    var laterFile = GetLaterFile(sourceDirectory.DirectoryPath!, true, latestInstance.LastWriteLocal);

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
        /// Work out which source directory resulted in this archive file, a slightly embarrassing bit of
        /// reverse engineering but I don't want to be storing the source directory name in the archive file
        /// or elsewhere, and this reliably does the job. It could fail if the source directory specification 
        /// changes but this is only used for reporting how many versions of an archive exist so there are 
        /// no horrific consequenses if that happens.
        /// </summary>
        /// <param name="baseFileName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private SourceDirectory? FindSourceDirectory(string baseFileName)
        {
            try
            {
                var matches = _jobSpec.SourceDirectories.Where(_ => _.IsEnabled && _.DirectoryPath!.EndsWith(baseFileName));

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
                .Where(_ => _.Type == enArchiveActionType.Delete)
                .OrderBy(_ => _.SourceFile!.FullName)
                .Select(_ => _.SourceFile);

            foreach (var file in filesToDelete)
            {
                result.AddWarning($"Deleting file '{file!.FullName}', {FileUtilities.GetByteSizeAsText(file.Length)}, last write {file.LastWriteTimeLocal.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)}");
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
                .Where(_ => _.Type == enArchiveActionType.Copy)
                .OrderByDescending(_ => _.SourceFile!.IslatestVersion)
                .OrderBy(_ => _.SourceFile!.DirectoryPriority)
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
                result.SubsumeResult(await DeleteTemporaryFilesInDirectory(destination.DirectoryPath!, false));

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

                // Don't bother with the snazzy progress display for small files
                bool showProgress = srcFil.Length > (srcFil.IsOnSlowVolume ? 1 * 1024 * 1024 : 5 * 1024 * 1024);

                Progress<KeyValuePair<long, long>> progressReporter = new();

                if (showProgress)
                {
                    LogEntry progressLogEntry = new(
                        percentComplete: 0,
                        prefix: $"Copying {srcFil.FileName}",
                        suffix: $"of {FileUtilities.GetByteSizeAsText(srcFil.Length)} to {dstDir.BaseDirectory!.DirectoryPath}"
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
                }

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

                    var srcTicks = srcFil.LastWriteTimeLocal.Ticks;

                    var fiDest = new FileInfo(dstFullName)
                    {
                        LastWriteTime = srcFil.LastWriteTimeLocal,
                        CreationTime = srcFil.CreationTimeLocal
                    };

                    var dstTicks = fiDest.LastWriteTime.Ticks;

                    // If we copy between different disk formats we can lose some accuracy, eg
                    // from NTFS to exFAT. We now round to the nearest second on creating the
                    // initial comnpressed files, so this should no longer happen

                    if (srcTicks != dstTicks)
                    {
                        result.AddWarning($"LastWrite ticks differ {new FileInfo(srcFil.FullName).LastWriteTime.ToString(Constants.DATE_FORMAT_DATE_TIME_YYYYMMDDHHMMSS)} is {srcTicks} & {fiDest.LastWriteTime.ToString(Constants.DATE_FORMAT_DATE_TIME_YYYYMMDDHHMMSS)} is {dstTicks}");
                    }
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
        /// Find any file written after the supplied time, we don't need the latest file, or more 
        /// than one, just the first one we find that fits. Returns null if no later files exist
        /// </summary>
        /// <param name="directoryName"></param>
        /// <param name="recursive"></param>
        /// <param name="laterThanUtc"></param>
        /// <returns></returns>
        internal static FileInfo? GetLaterFile(string directoryName, bool recursive, DateTime laterThanLocal)
        {
            DirectoryInfo root = new(directoryName);

            // We're only looking for the first file we find with a later timestamp, let's
            // try the files in root first, even if we're recursive

            var rootFiles = root.GetFiles("*.*", SearchOption.TopDirectoryOnly);

            var later = rootFiles.FirstOrDefault(_ => _.LastWriteTime > laterThanLocal);

            if (later != null)
            {
                return later;
            }
            else if (recursive)
            {
                // Ah well, worth a try, check any subdirectories one by one. Best to do it
                // this way rather than GetFiles the whole lot recursivenly, there could be
                // thousands of them.
                
                // We don't need the full set, just one later file will do.

                foreach (var di in root.GetDirectories())
                {
                    // Recurse below each sub-directory, we could make a recursive function to
                    // just take each directory's root content one by one, which would be faster 

                    var allFiles = di.GetFiles("*.*", SearchOption.AllDirectories); 

                    later = allFiles.FirstOrDefault(_ => _.LastWriteTime > laterThanLocal);

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
