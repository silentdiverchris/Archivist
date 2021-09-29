using Archivist.Helpers;
using Archivist.Utilities;
using System;
using System.IO;

namespace Archivist.Classes
{
    internal class ArchiveFileInstance
    {
        private readonly Result _result;
        private readonly FileInfo _fileInfo;
        private readonly bool _isVersioned = false;
        private readonly int? _versionNumber = null;
        private readonly string? _baseFileName = null;        
        private readonly BaseDirectoryFiles? _directory = null;

        private readonly bool _ignored = false;

        // This was an afterthought so tacked on late in the day, ought to be determined on the fly rather than updated later TODO
        private bool _isLatestVersion = false;
        
        internal ArchiveFileInstance(string filePath, BaseDirectoryFiles? directory, bool ignored, bool isLatestVersion, Result result)
        {
            _result = result;
            _isLatestVersion = isLatestVersion;

            if (directory is not null)
            {
                _directory = directory;
            }

            if (filePath.Contains(System.IO.Path.DirectorySeparatorChar))
            {
                var fi = new FileInfo(filePath);

                if (fi.Exists)
                {
                    _fileInfo = fi;
                    _ignored = ignored || fi.Extension == ".compressing" || fi.Extension == ".copying"; // TODO Formalise these extensions
                    _isVersioned = FileVersionHelpers.IsVersionedFileName(fi.Name);

                    if (IsVersioned)
                    {
                        _baseFileName = FileVersionHelpers.GetBaseFileName(fi.Name);
                        _versionNumber = FileVersionHelpers.ExtractVersionNumber(fi.Name);
                    }
                }
                else
                {
                    throw new ArgumentException($"VersionedFileInstance constructor supplied with non-existant file '{filePath}'");
                }
            }
            else
            {
                throw new ArgumentException($"VersionedFileInstance constructor supplied with filePath that has no directory names '{filePath}'");
            }
        }

        internal bool Ignored => _ignored;
        internal string FileName => _fileInfo.Name;
        internal DateTime LastWriteTimeLocal => _fileInfo.LastWriteTime;
        internal string FullName => _fileInfo.FullName;
        internal long Length => _fileInfo.Length;
        internal string? BaseFileName => _baseFileName;
        internal int? VersionNumber => _versionNumber;
        internal BaseDirectoryFiles? BaseDirectory => _directory;
        internal short DirectoryPriority => _directory?.Priority ?? 99;
        internal bool IsVersioned => _isVersioned;
        internal bool IsOnSlowVolume => _directory?.IsSlowVolume ?? false;
        internal bool IslatestVersion => _isLatestVersion;
        internal DateTime LastWriteTimeUtc => _fileInfo.LastWriteTimeUtc;
        internal DateTime CreationTimeUtc => _fileInfo.CreationTimeUtc;
        internal Result Result => _result;

        internal void SetIsLatestVersion()
        {
            _isLatestVersion = true;
        }

        internal bool IsOlderThanDays(int days)
        {
            return FileUtilities.IsOlderThanDays(_fileInfo.FullName, days, out _, out _);
        }

    }

}
