using Archivist.Classes;
using Archivist.Models;
using System;
using System.IO;

namespace Archivist.Helpers
{
    /// <summary>
    /// Encapsulates the concept of versioned files, these have a fixed file name format 
    /// of ..\RootFileName-nnnn.zip where nnnn is four digits
    /// </summary>
    public static class FileVersionHelpers
    {
        internal static Result GenerateNextVersionedFileNames(
            this string baseFileName, 
            SourceDirectory sourceDirectory,
            string archiveDirectoryPath, 
            int currentVersionNumber, 
            out string? nextFilePathZipped, 
            out string? nextFilePathEncrypted)
        {
            Result result = new("GenerateNextVersionedFileNames");

            if (currentVersionNumber >= 0 && currentVersionNumber <= 9999)
            {
                int nextVersionNumber = currentVersionNumber + 1;

                string errorPrefix = $"Current version number for '{baseFileName}' is {currentVersionNumber}, ";

                if (currentVersionNumber == 9999)
                {
                    nextFilePathZipped = null;
                    nextFilePathEncrypted = null;

                    result.AddError(errorPrefix + "not generating the next file name, you need to manually renumber the existing files to a lower numbers, ideally 0001.");
                }
                else
                {
                    if (currentVersionNumber > 9900)
                    {
                        result.AddWarning(errorPrefix + "this will break when it reaches 9999, before then you need to manually renumber the existing files, ideally to start again at 0001.");
                    }

                    string nextPath = Path.Combine(archiveDirectoryPath, $"{baseFileName}-{nextVersionNumber:0000}");
                    nextFilePathZipped = nextPath + ".zip";

                    nextFilePathEncrypted = sourceDirectory.EncryptOutput
                        ? nextPath + ".aes"
                        : null;                    
                }
            }
            else
            {
                throw new ArgumentException($"GenerateVersionedFileNames given invalid currentVersion {currentVersionNumber}");
            }

            return result;
        }

        /// <summary>
        /// The version suffix is of the form -nnnn.zip, so for root file name abcde.zip the versioned name 
        /// would be abcde-nnnn.zip where nnnn is four digits.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        internal static bool IsVersionedFileName(this string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || fileName.Length <= 9)
            {
                return false;
            }

            if (fileName[^4] == '.' && fileName[^9] == '-')
            {
                return fileName.ExtractVersionNumber() > 0;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Extract the version number from a versioned file name, see IsVersionedFileName for explanation
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        internal static int ExtractVersionNumber(this string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || fileName.Length <= 9)
            {
                return -1;
            }

            string numbers = fileName[^8..^4];

            if (numbers.IsDigits())
            {
                return int.Parse(numbers);
            }
            else
            {
                return -2;
            }
        }

        /// <summary>
        /// Get whether this is a versioned file name and optionally get the base file name
        /// </summary>
        /// <param name="fileName">Full path or just file name</param>
        /// <param name="baseFileName"></param>
        /// <returns></returns>
        //internal static bool IsVersionedFileName(this string fileName, out string baseFileName)
        //{
        //    try
        //    {
        //        bool isVersioned = fileName.IsVersionedFileName();
                
        //        baseFileName = GetBaseFileName(fileName); 

        //        return isVersioned;
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new Exception($"IsVersionedFileName file '{fileName}' exception {ex.Message}", ex);
        //    }
        //}

        internal static bool IsVersionedFile(this FileInfo fi)
        {
            return fi.Name.IsVersionedFileName();
        }

        /// <summary>
        /// Get the base file name from a full file path, i.e. the file name without any versioning or extension
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        internal static string GetBaseFileName(string filePath)
        {
            string baseFileName = filePath;

            if (baseFileName.Contains(Path.DirectorySeparatorChar))
            {
                int fileNameStart = baseFileName.LastIndexOf(Path.DirectorySeparatorChar) + 1;
                baseFileName = baseFileName[fileNameStart..];
            }

            if (IsVersionedFileName(baseFileName))
            {
                baseFileName = baseFileName[0..^9];
            }
            else
            {
                baseFileName = baseFileName[0..^4];
            }

            return baseFileName;
        }
    }
}
