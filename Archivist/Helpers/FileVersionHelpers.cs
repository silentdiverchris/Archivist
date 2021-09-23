using System;
using System.IO;

namespace Archivist.Helpers
{
    public static class FileVersionHelpers
    {
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
                return fileName.GetVersionNumber() > 0;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Extract the version number from a vaersioned file name, see IsVersionedFileName for explanation
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        internal static int GetVersionNumber(this string fileName)
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

        private static string GetVersionRoot(this string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || fileName.Length <= 9)
            {
                return fileName;
            }

            return fileName[0..^9] + fileName[^4..];
        }

        /// <summary>
        /// Get whether this is a versioned file name and optionally get the root file name
        /// </summary>
        /// <param name="fileName">Full path or just file name</param>
        /// <param name="rootName"></param>
        /// <returns></returns>
        internal static bool IsVersionedFileName(this string fileName, out string rootName)
        {

            try
            {
                bool isVersioned = fileName.IsVersionedFileName();
                
                if (isVersioned)
                {
                    rootName = fileName.GetVersionRoot();
                }
                else
                {
                    if (fileName.Contains(Path.DirectorySeparatorChar))
                    {
                        rootName = fileName[(fileName.LastIndexOf(Path.DirectorySeparatorChar) + 1)..];
                    }
                    else
                    {
                        rootName = fileName;
                    }
                }

                return isVersioned;
            }
            catch (Exception ex)
            {
                throw new Exception($"IsVersionedFileName file '{fileName}' exception {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get whether this is a versioned file
        /// </summary>
        /// <param name="fi"></param>
        /// <returns></returns>
        internal static bool IsVersionedFile(this FileInfo fi)
        {
            return fi.Name.IsVersionedFileName();
        }

        /// <summary>
        /// Get whether this is a versioned file and optionally get the root file name
        /// </summary>
        /// <param name="fi"></param>
        /// <param name="rootName"></param>
        /// <returns></returns>
        internal static bool IsVersionedFile(this FileInfo fi, out string rootName)
        {
            return fi.Name.IsVersionedFileName(out rootName);
        }
    }
}
