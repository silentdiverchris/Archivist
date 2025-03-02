﻿using Archivist.Classes;
using Archivist.Helpers;
using Archivist.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using static Archivist.Enumerations;

namespace Archivist.Services
{
    /// <summary>
    /// The service responsible for compressing archive files, uses System.IO.Compression to create zip files
    /// </summary>
    internal class CompressionService : BaseService
    {
        private readonly string _aesEncryptExecutable;

        internal CompressionService(
            Job jobSpec,
            AppSettings appSettings,
            LogService logService,
            string aesEncryptExecutable) : base(jobSpec, appSettings, logService)
        {
            if (aesEncryptExecutable is null || !File.Exists(aesEncryptExecutable))
            {
                throw new ArgumentException(aesEncryptExecutable is null
                    ? "No AESEncrypt executable path supplied"
                    : $"AESEncrypt executable {aesEncryptExecutable} does not exist");
            }

            _aesEncryptExecutable = aesEncryptExecutable;
        }

        internal async Task<Result> CompressSources()
        {
            Result result = new("CompressSources", false);

            await _logService.ProcessResult(result);

            if (Directory.Exists(_jobSpec.PrimaryArchiveDirectoryPath))
            {
                result.SubsumeResult(FileUtilities.CheckDiskSpace(_jobSpec.PrimaryArchiveDirectoryPath));

                var directoriesToCompress = _jobSpec.SourceDirectories
                    .Where(_ => _.IsToBeProcessed(_jobSpec))
                    .Where(_ => _jobSpec.ProcessTestOnly == false || _jobSpec.ProcessTestOnly == true && _.IsForTesting)
                    .OrderBy(_ => _.Priority)
                    .ThenBy(_ => _.DirectoryPath);

                result.Statistics.FileFound(directoriesToCompress.Count());

                foreach (var sourceDirectory in directoriesToCompress)
                {
                    if (sourceDirectory.IsAvailable)
                    {
                        Result compressResult = await MaybeCompressSource(sourceDirectory, _jobSpec.PrimaryArchiveDirectoryPath);

                        result.SubsumeResult(compressResult);

                        await _logService.ProcessResult(result, reportCompletion: false, reportItemCounts: false);

                        // Don't allow one failed operation to stop the rest being processed

                        result = new("CompressSources", false);
                    }
                    else
                    {
                        result.AddWarning($"CompressSources found source {sourceDirectory.DirectoryPath} is not available");
                    }

                    if (result.HasErrors)
                        break;
                }
            }
            else
            {
                result.AddError($"CompressSources found primary archive directory {_jobSpec.PrimaryArchiveDirectoryPath} does not exist");
            }

            await _logService.ProcessResult(result, reportCompletion: true, reportItemCounts: true);

            return result;
        }
        /// <summary>
        /// Doesn't do anything yet...
        /// </summary>
        /// <param name="archiveRegister"></param>
        /// <returns></returns>
        internal async Task<Result> ExecuteFileCompressionActions(ArchiveRegister archiveRegister)
        {
            Result result = new("ExecuteFileCompressionActions", false);

            var dirsToCompress = archiveRegister.Actions
                .Where(_ => _.Type == enArchiveActionType.Compress);

            foreach (var file in dirsToCompress)
            {

            }

            await _logService.ProcessResult(result);

            return result;
        }

        internal async Task<Result> MaybeCompressSource(SourceDirectory sourceDirectory, string archiveDirectoryPath)
        {
            Result result = new("CompressSource", true, $"'{sourceDirectory.DirectoryPath}'");

            bool needToArchive = true;

            if (sourceDirectory.CheckTasksNotRunning is not null)
            {
                foreach (var taskName in sourceDirectory.CheckTasksNotRunning)
                {
                    var procList = Process.GetProcessesByName(taskName);

                    if (procList.Any())
                    {
                        result.AddWarning($"{taskName} is running, skipping archive");
                        needToArchive = false;
                    }
                    else
                    {
                        result.AddDebug($"{taskName} is not running");
                    }
                }
            }

            if (needToArchive)
            {
                if (sourceDirectory.IsAvailable)
                {
                    foreach (var tempFile in new DirectoryInfo(archiveDirectoryPath!).GetFiles("*.compressing"))
                    {
                        result.AddInfo($"Deleting old temporary file '{tempFile.Name}'");
                        tempFile.Delete();
                    }

                    Result generateOutputFileNameResult = PrepareFileNames(
                        sourceDirectory: sourceDirectory,
                        archiveDirectoryPath: archiveDirectoryPath,
                        baseOutputFileName: out string baseOutputFileName,
                        existingFilePathZipped: out string? existingFilePathZipped,
                        existingFilePathEncrypted: out string? existingFilePathEncrypted,
                        nextFilePathZipped: out string? nextFilePathZipped,
                        nextFilePathEncrypted: out string? nextFilePathEncrypted,
                        latestLastWriteLocal: out DateTime? latestArchiveWriteLocal);

                    result.SubsumeResult(generateOutputFileNameResult);

                    if (generateOutputFileNameResult.HasNoErrors)
                    {
                        if (latestArchiveWriteLocal is not null)
                        {
                            // We have an existing archive, does it need updating ?

                            var lastWriteThresholdLocal = (DateTime)latestArchiveWriteLocal + new TimeSpan(0, sourceDirectory.MinutesOldThreshold, 0);

                            result.AddDebug($"Processing archive '{baseOutputFileName}' last written {((DateTime)latestArchiveWriteLocal).ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)}");
                            result.AddDebug($"Looking for files written after {lastWriteThresholdLocal.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)}");

                            using (var fileService = new FileService(_jobSpec, _appSettings, _logService))
                            {
                                var fiLater = FileService.GetLaterFile(sourceDirectory.DirectoryPath!, true, lastWriteThresholdLocal);

                                if (fiLater is not null)
                                {
                                    result.AddDebug($"Found a later file in '{sourceDirectory.DirectoryPath}', '{fiLater.FullName}' last written {fiLater.LastWriteTime.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)}");
                                }
                                else
                                {
                                    result.AddDebug($"Found no later files in '{sourceDirectory.DirectoryPath}'");
                                    needToArchive = false;
                                }
                            }
                        }
                        else
                        {
                            result.AddInfo($"Found no existing archive for '{sourceDirectory.DirectoryPath}'");
                        }

                        if (needToArchive)
                        {
                            result.AddInfo($"Archiving '{sourceDirectory.DirectoryPath}' to '{nextFilePathZipped}' ({sourceDirectory.CompressionLevel})");
                            await _logService.ProcessResult(result);

                            try
                            {
                                string tempDestFileName = nextFilePathZipped + ".compressing";

                                if (File.Exists(tempDestFileName))
                                {
                                    File.Delete(tempDestFileName);
                                }

                                using (FileStream zipToOpen = new FileStream(tempDestFileName, FileMode.Create))
                                using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                                {
                                    foreach (var file in Directory.GetFiles(sourceDirectory.DirectoryPath!))
                                    {
                                        var entryName = Path.GetFileName(file);
                                        var entry = archive.CreateEntry(entryName);
                                        entry.LastWriteTime = File.GetLastWriteTime(file);
                                        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                        using (var stream = entry.Open())
                                        {
                                            fs.CopyTo(stream);
                                        }
                                    }
                                }

                                //ZipFile.CreateFromDirectory(sourceDirectory.DirectoryPath!, tempDestFileName, (CompressionLevel)sourceDirectory.CompressionLevel, true);

                                if (File.Exists(tempDestFileName))
                                {
                                    File.Move(tempDestFileName, nextFilePathZipped!, true);
                                }

                                var fiOutput = new FileInfo(nextFilePathZipped!);

                                if (fiOutput.Exists)
                                {
                                    result.AddSuccess($"Zipped {sourceDirectory.DirectoryPath} to {nextFilePathZipped} OK");

                                    FileUtilities.RoundFileTimes(nextFilePathZipped!);

                                    result.Statistics.FiledAdded(fiOutput.Length);
                                    _jobSpec.PrimaryArchiveStatistics.FiledAdded(fiOutput.Length);

                                    if (sourceDirectory.EncryptOutput)
                                    {
                                        using (var encryptionService = new EncryptionService(_jobSpec, _appSettings, _logService))
                                        {
                                            Result encryptionResult = await encryptionService.EncryptFileAsync(
                                                aesEncryptExecutable: _aesEncryptExecutable,
                                                sourceFileName: nextFilePathZipped!,
                                                destinationFileName: nextFilePathEncrypted,
                                                password: _jobSpec.EncryptionPassword);

                                            result.SubsumeResult(encryptionResult, addStatistics: false);
                                        }
                                    }
                                }
                            }
                            catch (Exception zipException)
                            {
                                result.AddException(zipException);
                            }
                        }
                        else
                        {
                            result.AddDebug($"Archive '{baseOutputFileName}' or '{sourceDirectory.DirectoryPath}' does not need updating");
                        }
                    }
                    else
                    {
                        result.AddError($"Failed to generate output file name for '{sourceDirectory.DirectoryPath}'");
                    }
                }
                else
                {
                    result.AddError($"CompressSource found directory '{sourceDirectory.DirectoryPath}' does not exist");
                }
            }

            await _logService.ProcessResult(result, reportItemCounts: false, reportAllStatistics: true);

            return result;
        }

        /// <summary>
        /// Determine the output file names that this directory should create (encrypted and not), the name of the 
        /// current latest one that exists, if any, and the names of the next ones that should be created
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <param name="archiveDirectoryName"></param>
        /// <returns></returns>
        private Result PrepareFileNames(
            SourceDirectory sourceDirectory,
            string archiveDirectoryPath,
            out string baseOutputFileName,
            out string? existingFilePathZipped,
            out string? existingFilePathEncrypted,
            out string? nextFilePathZipped,
            out string? nextFilePathEncrypted,
            out DateTime? latestLastWriteLocal)
        {
            Result result = new("GenerateOutputFileNames", false);

            existingFilePathZipped = null;
            existingFilePathEncrypted = null;
            nextFilePathZipped = null;
            nextFilePathEncrypted = null;
            latestLastWriteLocal = null;

            baseOutputFileName = FileUtilities.GenerateBaseOutputFileName(sourceDirectory);

            try
            {
                var existingFiles = archiveDirectoryPath.GetVersionedFiles(baseOutputFileName);

                int currentVersionNumber = 0;

                if (existingFiles.Any())
                {
                    // The list is sorted alpha, so whatever the extension, the last in the list is always the latest

                    var lastFileName = existingFiles.Last();

                    latestLastWriteLocal = new FileInfo(lastFileName).LastWriteTime;

                    if (lastFileName.EndsWith(".zip"))
                    {
                        existingFilePathZipped = lastFileName;
                        existingFilePathEncrypted = null;
                    }
                    else if (lastFileName.EndsWith(".aes"))
                    {
                        existingFilePathZipped = null;
                        existingFilePathEncrypted = lastFileName;
                    }
                    else
                    {
                        result.AddError($"GenerateOutputFileNames found invalid last file '{lastFileName}'");
                    }

                    currentVersionNumber = lastFileName.ExtractVersionNumber();
                }
                else
                {
                    existingFilePathZipped = null;
                    existingFilePathEncrypted = null;
                }

                Result fileNameResult = baseOutputFileName.GenerateNextVersionedFileNames(sourceDirectory, archiveDirectoryPath, currentVersionNumber: currentVersionNumber, out nextFilePathZipped, out nextFilePathEncrypted);

                result.SubsumeResult(fileNameResult);

                if (fileNameResult.HasErrors)
                {
                    result.AddError($"GenerateOutputFileName failed to generate new versioned file names for currentNumber {currentVersionNumber}, source directory {sourceDirectory.DirectoryPath}");
                }
            }
            catch (Exception ex)
            {
                result.AddException(ex);
            }

            return result;
        }

        internal new void Dispose()
        {
            base.Dispose();
        }
    }
}
