using Archivist.Classes;
using Archivist.Models;
using System.Diagnostics;

namespace Archivist.Services
{
    internal class EncryptionService : BaseService
    {       
        internal EncryptionService(
            Job jobSpec,
            LogService logService) : base(jobSpec, logService)
        {
        }

        internal async Task<Result> EncryptFileAsync(string aesEncryptExecutable, string sourceFileName, string destinationFileName = null, string password = null, bool deleteSourceFile = false, bool synchroniseTimestamps = true)
        {
            Result result = new("EncryptFileAsync", false);

            if (File.Exists(sourceFileName) && sourceFileName.ToLower() != "clue.txt")
            {
                FileInfo fiSrc = new(sourceFileName);

                if (destinationFileName is null)
                {
                    destinationFileName = sourceFileName + ".aes";
                }
                
                ProcessStartInfo procInfo = new()
                {
                    FileName = aesEncryptExecutable,
                    Arguments = $"-e -p{password} -o\"{destinationFileName}\" \"{sourceFileName}\""
                };

                result.AddInfo($"AESCrypt encrypting '{fiSrc.Name}'"); // to '{destinationFileName}'");

                Process process = Process.Start(procInfo);

                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    if (result.HasNoErrorsOrWarnings && File.Exists(destinationFileName))
                    {
                        result.Statistics.ItemsProcessed++;
                        result.Statistics.BytesProcessed += fiSrc.Length;

                        result.Statistics.FilesAdded++;
                        result.Statistics.BytesAdded += fiSrc.Length;

                        result.AddSuccess($"Encrypted to {destinationFileName} OK");

                        if (synchroniseTimestamps)
                        {
                            FileInfo fiDest = new(destinationFileName);
                            fiDest.CreationTimeUtc = fiSrc.CreationTimeUtc;
                            fiDest.LastWriteTimeUtc = fiSrc.LastWriteTimeUtc;
                        }

                        if (deleteSourceFile)
                        {
                            result.AddInfo($"Deleting unencrypted source {sourceFileName}");
                            File.Delete(sourceFileName);
                        }
                    }
                }
                else
                {
                    result.AddError($"EncryptFileAsync calling AESEncrypt failed with exit code {process.ExitCode}");
                }
            }
            else
            {
                result.AddError($"EncryptFileAsync found {sourceFileName} does not exist");
            }

            await _logService.ProcessResult(result, reportCompletion: false, false);

            return result;
        }

        internal new void Dispose()
        {
            base.Dispose();
        }
    }
}
