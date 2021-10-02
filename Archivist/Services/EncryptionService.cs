using Archivist.Classes;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Archivist.Services
{
    /// <summary>
    /// The service that encrypts archives, uses external executable AESEncrypt
    /// </summary>
    internal class EncryptionService : BaseService
    {
        internal EncryptionService(
            Job jobSpec,
            AppSettings appSettings,
            LogService logService) : base(jobSpec, appSettings, logService)
        {
        }

        internal async Task<Result> EncryptFileAsync(
            string aesEncryptExecutable,
            string sourceFileName,
            string? destinationFileName = null,
            string? password = null,
            bool deleteSourceFile = true,
            bool synchroniseTimestamps = true)
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

                result.AddInfo($"AESCrypt encrypting '{fiSrc.Name}' to '{destinationFileName}'");

                Process? process = Process.Start(procInfo);

                if (process is not null)
                {
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        if (result.HasNoErrorsOrWarnings && File.Exists(destinationFileName))
                        {
                            result.AddSuccess($"Encrypted to {destinationFileName} OK");

                            result.Statistics.FiledAdded(fiSrc.Length);
                            result.Statistics.FileDeleted(fiSrc.Length);

                            if (synchroniseTimestamps)
                            {
                                FileInfo fiDest = new(destinationFileName);
                                fiDest.CreationTimeUtc = fiSrc.CreationTimeUtc;
                                fiDest.LastWriteTimeUtc = fiSrc.LastWriteTimeUtc;
                            }

                            result.AddInfo($"Deleting unencrypted source {sourceFileName}");

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
                    result.AddError($"EncryptFileAsync failed to generate process to call AESEncrypt");
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
