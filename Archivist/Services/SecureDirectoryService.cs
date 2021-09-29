using Archivist.Classes;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Archivist.Services
{
    internal class SecureDirectoryService : BaseService
    {
        private readonly string _aesEncryptExecutable;

        internal SecureDirectoryService(
            Job jobSpec,
            AppSettings appSettings,
            LogService logService,
            string aesEncryptExecutable) : base(jobSpec, appSettings, logService)
        {
            if (string.IsNullOrEmpty(aesEncryptExecutable))
            {
                throw new ArgumentException("No AESEncrypt executable path supplied");
            }

            if (!File.Exists(aesEncryptExecutable))
            {
                throw new ArgumentException($"AESEncrypt executable '{aesEncryptExecutable}' does not exist");
            }

            _aesEncryptExecutable = aesEncryptExecutable;
        }

        internal async Task<Result> ProcessSecureDirectories()
        {
            Result result = new("ProcessSecureDirectories", false);

            if (_jobSpec.SecureDirectories != null)
            {
                foreach (var secureDirectory in _jobSpec.SecureDirectories
                    .Where(_ => _.IsToBeProcessed(_jobSpec)))
                {
                    Result processResult = await ProcessSecureDirectory(secureDirectory);
                    result.SubsumeResult(processResult);
                }
            }

            await _logService.ProcessResult(result, reportCompletion: true, reportItemCounts: false);

            return result;
        }

        /// <summary>
        /// This non-recursively processes a folder containing files that generally want be encrypted at rest and should always always encrypted in
        /// backups. The idea is to have a folder containing a bunch of AES encrypted files, which are .aes versions of whatever is in there, eg. bank 
        /// details might be in a file called BankDetails.txt, this will be encrypted to BankDetails.txt.aes. 
        ///
        /// As new files are added they will be encrypted by this function when a backup is run, same with files that are manually unencrypted to 
        /// be read or updated.
        ///
        /// The generated .aes files will have both last write and creation timestamps set to be identical to the original, we use the LastWriteTime 
        /// one to make decisions
        /// 
        /// Check for unencrypted files in the path which..
        /// ..have no encrypted version; encrypt them and remove the original (handle the creation of a new unencrypted file)
        /// ..have a later or identical timestamped .aes version; delete them (handle where a file has been unencrypted to be read, but not changed, just tidying up)
        /// ..have an earlier .aes version; re-encrypt them and delete original (handle where the file has been unencrypted and changed, updating encrypted version)
        /// 
        /// </summary>
        /// <param name="directoryName"></param>
        /// <returns></returns>
        private async Task<Result> ProcessSecureDirectory(SecureDirectory secureDirectory)
        {
            Result result = new("ProcessSecureDirectory", true);

            result.AddInfo($"Securing directory {secureDirectory.DirectoryPath}");

            await _logService.ProcessResult(result);

            if (secureDirectory.IsAvailable)
            {
                var filesToProcess = Directory.GetFiles(secureDirectory.DirectoryPath!)
                        .Where(_ => _.ToLower().EndsWith(".aes") == false)
                        .Where(_ => _.ToLower().EndsWith("clue.txt") == false);

                if (filesToProcess.Any())
                {
                    result.Statistics.FileFound(filesToProcess.Count());

                    using (var encryptionService = new EncryptionService(_jobSpec, _appSettings, _logService))
                    {
                        foreach (var fullFileName in filesToProcess)
                        {
                            var fiSrc = new FileInfo(fullFileName);
                            var encFileName = fiSrc.FullName + ".aes";

                            bool doEncryption = false;

                            if (File.Exists(encFileName))
                            {
                                var fiEnc = new FileInfo(encFileName);

                                if (fiSrc.LastWriteTimeUtc > fiEnc.LastWriteTimeUtc)
                                {
                                    // Unencrypted version has been changed, make a new encrypt
                                    doEncryption = true;
                                }
                                else if (fiSrc.LastWriteTimeUtc == fiEnc.LastWriteTimeUtc)
                                {
                                    // Encrypted version exists, unencrypted has same last write time, just remove the unencrypted one
                                    doEncryption = false;
                                }
                                else if (fiSrc.LastWriteTimeUtc <= fiEnc.LastWriteTimeUtc)
                                {
                                    // Encrypted version is later, must have been done manually, lets recreate it just to be on the safe side
                                    doEncryption = true;
                                }
                                else
                                {
                                    throw new Exception($"ProcessSecureDirectory: something odd happened with {secureDirectory.DirectoryPath}");
                                }
                            }
                            else
                            {
                                doEncryption = true;
                            }

                            if (doEncryption)
                            {
                                result.AddInfo($"Securing file {fiSrc.FullName}");

                                Result encryptResult = await encryptionService.EncryptFileAsync(
                                    aesEncryptExecutable:  _aesEncryptExecutable,
                                    sourceFileName: fiSrc.FullName,
                                    destinationFileName: null,
                                    password: _jobSpec.EncryptionPassword);

                                await _logService.ProcessResult(encryptResult);

                                result.SubsumeResult(encryptResult);

                                if (encryptResult.HasNoErrors)
                                {
                                    result.Statistics.FileDeleted(fiSrc.Length);
                                    result.AddInfo($"Deleting unencrypted source {fiSrc.FullName}");
                                    fiSrc.Delete();
                                }
                            }
                        }
                    }
                }
                else
                {
                    result.AddInfo("No files found to encrypt");
                }
            }
            else
            {
                result.AddWarning($"ProcessSecureDirectoryAsync found secure directory {secureDirectory.DirectoryPath} does not exist");
            }

            await _logService.ProcessResult(result, reportItemCounts: true, reportCompletion: false);

            return result;
        }

        internal new void Dispose()
        {
            base.Dispose();
        }
    }
}
