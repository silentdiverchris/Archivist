using Archivist.Classes;
using Archivist.Helpers;
using Archivist.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace Archivist.Services
{
    internal class CompressionService : BaseService
    {
        internal int TotalArchivesCreated { get; private set; } = 0;
        internal long TotalBytesArchived { get; private set; } = 0;

        private readonly string _aesEncryptExecutable;

        internal CompressionService(
            JobSpecification jobSpec,
            LogService logService,
            string aesEncryptExecutable) : base(jobSpec, logService)
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

            if (Directory.Exists(_jobSpec.PrimaryArchiveDirectoryName))
            {
                var foldersToCompress = _jobSpec.SourceDirectories
                    .Where(_ => _.IsToBeProcessed(_jobSpec))
                    .OrderBy(_ => _.Priority)
                    .ThenBy(_ => _.DirectoryPath);

                result.ItemsFound = foldersToCompress.Count();

                foreach (var sourceDirectory in foldersToCompress)
                {
                    if (sourceDirectory.IsAvailable)
                    {
                        Result compressResult = await CompressSourceDirectoryAsync(sourceDirectory, _jobSpec.PrimaryArchiveDirectoryName);

                        result.SubsumeResult(compressResult);
                    }
                    else
                    {
                        result.AddWarning($"CompressSources found source {sourceDirectory.DirectoryPath} does not exist");
                    }

                    if (result.HasErrors)
                        break;
                }

                if (result.HasNoErrorsOrWarnings)
                {
                    result.AddSuccess("Processed source directories OK");
                }
            }
            else
            {
                result.AddError($"CompressSources found primary archive directory {_jobSpec.PrimaryArchiveDirectoryName} does not exist");
            }

            await _logService.ProcessResult(result, addCompletionItem: true, reportItemCounts: true, "file");

            return result;
        }

        internal async Task<Result> CompressSourceDirectoryAsync(SourceDirectory sourceDirectory, string archiveDirectoryName)
        {
            Result result = new("CompressSourceDirectoryAsync", true, $"for source '{sourceDirectory.DirectoryPath}', destination '{archiveDirectoryName}'");

            if (sourceDirectory.IsAvailable)
            {
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

                            result.AddDebug($"Archive '{currentOutputFileName}' last written {fiCurrentArchive.LastWriteTimeUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} UTC, looking for files written after {lastWriteThreshold.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)}");

                            using (var fileService = new FileService(_jobSpec, _logService))
                            {
                                var fiLater = FileService.GetLaterFile(sourceDirectory.DirectoryPath, true, lastWriteThreshold);

                                if (fiLater is not null)
                                {
                                    result.AddDebug($"Found a later file '{fiLater.FullName}' at {fiLater.LastWriteTimeUtc.ToString(Constants.DATE_FORMAT_DATE_TIME_LONG_SECONDS)} in '{sourceDirectory.DirectoryPath}'");
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
                        //result.AddDebug($"Decided to archive '{sourceDirectory.DirectoryPath}'");

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
                                result.ItemsProcessed++;
                                result.BytesProcessed += fiOutput.Length;

                                if (movedFileName is not null)
                                {
                                    File.Delete(movedFileName);
                                }

                                result.AddSuccess($"Zipped {sourceDirectory.DirectoryPath} to {outputFileName} OK");

                                TotalBytesArchived += fiOutput.Length;
                                TotalArchivesCreated++;

                                if (sourceDirectory.EncryptOutput)
                                {
                                    using (var encryptionService = new EncryptionService(_jobSpec, _logService))
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

                                if (sourceDirectory.RetainVersions > 0 && FileService.FileNameMatchesVersionedPattern(fiOutput.FullName))
                                {
                                    using (var fileService = new FileService(_jobSpec, _logService))
                                    {
                                        Result deleteResult = await fileService.DeleteOldVersions(outputFileName, sourceDirectory.RetainVersions);

                                        result.SubsumeResult(deleteResult);
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

            await _logService.ProcessResult(result);

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
                string fileName = sourceDirectory.OutputFileName ?? GenerateFileNameFromPath(sourceDirectory.DirectoryPath);
                currentFileName = $"{archiveDirectoryName}\\{fileName}";

                if (sourceDirectory.AddVersionSuffix)
                {
                    // The suffix is of the form -nnnn.zip, so for file abcde.zip we are looking for abcde-nnnnn.zip

                    int expectedFilenameLength = archiveDirectoryName.Length + 1 + fileName.Length + 5;

                    var existingFiles = Directory.GetFiles(archiveDirectoryName, fileName.Replace(".zip", "*.zip"))
                        .Where(_ => _.Length == expectedFilenameLength)
                        .OrderBy(_ => _);

                    if (existingFiles.Any())
                    {
                        var lastFileName = existingFiles.Last();

                        if (lastFileName.Length > 10)
                        {
                            var hyphen = lastFileName.Substring(lastFileName.Length - 9, 1);
                            var numbers = lastFileName.Substring(lastFileName.Length - 8, 4);

                            if (hyphen == "-" && StringHelpers.IsDigits(numbers))
                            {
                                int currentNumber = Int32.Parse(numbers);
                                int nextNumber = currentNumber + 1;

                                if (currentNumber == 9999)
                                {
                                    result.AddError($"Current version number for '{currentFileName}' is {currentNumber}, not generating the next file name, you need to manually renumber the existing files to a lower numbers, ideally 0001.");
                                }
                                else if (currentNumber > 9900)
                                {
                                    result.AddWarning($"Current version number for '{currentFileName}' is {currentNumber}, this will break when it reaches 9999 and you will need to manually renumber the existing files to a lower numbers, ideally 0001, best do that now.");
                                }

                                currentFileName = $"{archiveDirectoryName}\\" + fileName.Replace(".zip", $"-{currentNumber:0000}.zip");
                                result.ReturnedString = Path.Combine(archiveDirectoryName, fileName.Replace(".zip", $"-{nextNumber:0000}.zip"));
                            }
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

        private string GenerateFileNameFromPath(string directoryName)
        {
            var bits = directoryName.Split(Path.DirectorySeparatorChar);

            if (bits.Length > 1)
            {
                var fileName = string.Join("-", bits[1..]) + ".zip";
                return fileName;
            }
            else
            {
                throw new Exception($"GenerateFileNameFromPath found path {directoryName} too short");
            }
        }

        internal new void Dispose()
        {
            base.Dispose();
        }
    }
}
