using Archivist.Classes;
using Archivist.Helpers;
using Archivist.Models;
using Archivist.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace Archivist.Services
{
    /// <summary>
    /// The service responsible for compressing archive files, uses System.IO.Compression to create zip files
    /// </summary>
    internal class CompressionService : BaseService
    {
        internal int TotalArchivesCreated { get; private set; } = 0;
        internal long TotalBytesArchived { get; private set; } = 0;

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
            Result result = new("CompressSources", true);

            await _logService.ProcessResult(result);

            if (Directory.Exists(_jobSpec.PrimaryArchiveDirectoryName))
            {
                result.SubsumeResult(FileUtilities.CheckDiskSpace(_jobSpec.PrimaryArchiveDirectoryName));

                var foldersToCompress = _jobSpec.SourceDirectories
                    .Where(_ => _.IsToBeProcessed(_jobSpec))
                    .OrderBy(_ => _.Priority)
                    .ThenBy(_ => _.DirectoryPath);

                result.Statistics.FileFound(foldersToCompress.Count());

                foreach (var sourceDirectory in foldersToCompress)
                {
                    if (sourceDirectory.IsAvailable)
                    {
                        Result compressResult = await CompressSource(sourceDirectory, _jobSpec.PrimaryArchiveDirectoryName);

                        result.SubsumeResult(compressResult);
                    }
                    else
                    {
                        result.AddWarning($"CompressSources found source {sourceDirectory.DirectoryPath} does not exist");
                    }

                    if (result.HasErrors)
                        break;
                }
            }
            else
            {
                result.AddError($"CompressSources found primary archive directory {_jobSpec.PrimaryArchiveDirectoryName} does not exist");
            }

            await _logService.ProcessResult(result, reportCompletion: true, reportItemCounts: true);

            return result;
        }

        internal async Task<Result> CompressSource(SourceDirectory sourceDirectory, string archiveDirectoryName)
        {
            Result result = new("CompressSource", true, $"to '{archiveDirectoryName}' from '{sourceDirectory.DirectoryPath}'");

            if (sourceDirectory.IsAvailable)
            {
                foreach (var tempFile in new DirectoryInfo(archiveDirectoryName).GetFiles("*.compressing"))
                {
                    result.AddInfo($"Deleting old temporary file '{tempFile.Name}'");
                    tempFile.Delete();
                }

                bool needToArchive = true;
                string movedFileName = null;

                Result generateOutputFileNameResult = GenerateOutputFileName(sourceDirectory, archiveDirectoryName, out string currentOutputFileName);
                result.SubsumeResult(generateOutputFileNameResult);

                if (generateOutputFileNameResult.HasNoErrors)
                {
                    string outputFileName = generateOutputFileNameResult.ReturnedString;

                    if (sourceDirectory.CheckTaskNameIsNotRunning is not null)
                    {
                        var procList = Process.GetProcessesByName(sourceDirectory.CheckTaskNameIsNotRunning);

                        if (procList.Any())
                        {
                            result.AddWarning($"{sourceDirectory.CheckTaskNameIsNotRunning} is running, skipping archive");
                            needToArchive = false;
                        }
                        else
                        {
                            result.AddDebug($"{sourceDirectory.CheckTaskNameIsNotRunning} is not running");
                        }
                    }

                    if (needToArchive)
                    {
                        if (File.Exists(currentOutputFileName))
                        {
                            var fiCurrentArchive = new FileInfo(currentOutputFileName);

                            var lastWriteThreshold = fiCurrentArchive.LastWriteTimeUtc + new TimeSpan(0, sourceDirectory.MinutesOldThreshold, 0);

                            result.AddDebug($"Processing archive '{currentOutputFileName}' last written {fiCurrentArchive.LastWriteTimeUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} UTC");
                            result.AddDebug($"Looking for files written after {lastWriteThreshold.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} UTC");

                            using (var fileService = new FileService(_jobSpec, _appSettings, _logService))
                            {
                                var fiLater = FileService.GetLaterFile(sourceDirectory.DirectoryPath, true, lastWriteThreshold);

                                if (fiLater is not null)
                                {
                                    result.AddDebug($"Found a later file '{fiLater.FullName}' at {fiLater.LastWriteTimeUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} UTC in '{sourceDirectory.DirectoryPath}'");
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
                            result.AddInfo($"Found no existing '{currentOutputFileName}'");
                        }
                    }

                    if (needToArchive)
                    {
                        if (File.Exists(outputFileName))
                        {
                            if (sourceDirectory.ReplaceExisting)
                            {
                                movedFileName = outputFileName + ".tobereplaced";
                                File.Move(outputFileName, movedFileName);
                            }
                        }

                        result.AddInfo($"Archiving '{sourceDirectory.DirectoryPath}' to '{outputFileName}' ({sourceDirectory.CompressionLevel})");
                        await _logService.ProcessResult(result);

                        try
                        {
                            string tempDestFileName = outputFileName + ".compressing";

                            if (File.Exists(tempDestFileName))
                            {
                                File.Delete(tempDestFileName);
                            }

                            ZipFile.CreateFromDirectory(sourceDirectory.DirectoryPath, tempDestFileName, (CompressionLevel)sourceDirectory.CompressionLevel, true);

                            if (File.Exists(tempDestFileName))
                            {
                                File.Move(tempDestFileName, outputFileName, true);
                            }

                            var fiOutput = new FileInfo(outputFileName);

                            if (fiOutput.Exists)
                            {

                                if (movedFileName is not null)
                                {
                                    File.Delete(movedFileName);
                                }

                                result.AddSuccess($"Zipped {sourceDirectory.DirectoryPath} to {outputFileName} OK");

                                TotalBytesArchived += fiOutput.Length;
                                TotalArchivesCreated++;

                                result.Statistics.FiledAdded(fiOutput.Length);
                                _jobSpec.PrimaryArchiveStatistics.FiledAdded(fiOutput.Length);

                                if (sourceDirectory.EncryptOutput)
                                {
                                    using (var encryptionService = new EncryptionService(_jobSpec, _appSettings, _logService))
                                    {
                                        Result encryptionResult = await encryptionService.EncryptFileAsync(
                                            aesEncryptExecutable: _aesEncryptExecutable,
                                            sourceFileName: outputFileName,
                                            destinationFileName: null,
                                            password: _jobSpec.EncryptionPassword,
                                            deleteSourceFile: sourceDirectory.DeleteArchiveAfterEncryption);

                                        result.SubsumeResult(encryptionResult);
                                    }
                                }

                                if (sourceDirectory.RetainMinimumVersions >= Constants.RETAIN_VERSIONS_MINIMUM)
                                {
                                    if (fiOutput.IsVersionedFile())
                                    {
                                        using (var fileService = new FileService(_jobSpec, _appSettings, _logService))
                                        {
                                            result.SubsumeResult(
                                                await fileService.DeleteOldVersions(outputFileName, sourceDirectory.RetainMinimumVersions, sourceDirectory.RetainMaximumVersions, sourceDirectory.RetainYoungerThanDays));
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception zipException)
                        {
                            result.AddException(zipException);
                        }
                        finally
                        {
                            if (!File.Exists(outputFileName) & movedFileName is not null)
                            {
                                result.AddError($"Failed to generate '{outputFileName}', replacing the existing file");
                                File.Move(movedFileName, outputFileName);
                            }
                        }
                    }
                    else
                    {
                        result.AddDebug($"Archive '{outputFileName}' does not need updating");
                    }
                }
                else
                {
                    result.AddError($"Failed to generate output file anme for '{sourceDirectory.DirectoryPath}'");
                }
            }
            else
            {
                result.AddError($"CompressSourceDirectoryAsync found directory '{sourceDirectory.DirectoryPath}' does not exist");
            }

            await _logService.ProcessResult(result, reportItemCounts: false, reportAllStatistics: true);

            return result;
        }

        /// <summary>
        /// Determine the output file name to use
        /// </summary>
        /// <param name="sourceDirectory"></param>
        /// <param name="archiveDirectoryName"></param>
        /// <param name="getNextFileName">Controls whether it gets the current one or the next</param>
        /// <returns></returns>
        private Result GenerateOutputFileName(SourceDirectory sourceDirectory, string archiveDirectoryName, out string currentFileName)
        {
            Result result = new("GenerateOutputFileName", false);

            currentFileName = null;

            try
            {
                string fileName = sourceDirectory.OutputFileName ?? FileUtilities.GenerateFileNameFromPath(sourceDirectory.DirectoryPath);
                currentFileName = $"{archiveDirectoryName}\\{fileName}";

                if (sourceDirectory.AddVersionSuffix)
                {
                    // The suffix is of the form -nnnn.zip, so for file abcde.zip we are looking for abcde-nnnnn.zip

                    int expectedFilenameLength = archiveDirectoryName.Length + 1 + fileName.Length + 5;
                    string fileSpec = fileName.Replace(".zip", "*.zip");

                    // Oops, this is ignoring encrypted files, fix this TODO

                    var existingVersionedFiles = Directory.GetFiles(archiveDirectoryName, fileSpec)
                        .Where(_ => _.Length == expectedFilenameLength)
                        .Where(_ => _.IsVersionedFileName())
                        .OrderBy(_ => _);

                    if (existingVersionedFiles.Any())
                    {
                        var lastFileName = existingVersionedFiles.Last();

                        var currentNumber = lastFileName.GetVersionNumber();

                        string nextFileName = fileName.GenerateVersionedFileName(archiveDirectoryName: archiveDirectoryName, currentVersionNumber: currentNumber, out Result fileNameResult);

                        result.SubsumeResult(fileNameResult);

                        if (fileNameResult.HasNoErrors)
                        {
                            result.ReturnedString = nextFileName; 
                        }
                        else
                        {
                            result.AddError($"GenerateOutputFileName failed to generate new versioned file name for currentNumber {currentNumber}, lastFileName {lastFileName}");
                        }
                    }
                    else
                    {
                        currentFileName = $"{archiveDirectoryName}\\{fileName.Replace(".zip", "-0001.zip")}";
                        result.ReturnedString = $"{archiveDirectoryName}\\{fileName.Replace(".zip", "-0001.zip")}";
                    }
                }
                else
                {
                    result.ReturnedString = $"{archiveDirectoryName}\\{fileName}";
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
