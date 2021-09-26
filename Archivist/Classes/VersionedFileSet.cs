using Archivist.Helpers;
using System.Collections.Generic;
using System.Linq;

namespace Archivist.Classes
{
    internal class VersionedFileSets
    {
        private readonly List<VersionedFileSet> _sets = new();

        internal IEnumerable<string> BaseFileNames => _sets.Select(_ => _.BaseFileName);

        internal IEnumerable<string> VersionsOfFile(string baseFileName)
        {
            var set = _sets.SingleOrDefault(_ => _.BaseFileName == baseFileName);

            return set is null
                ? new List<string>()
                : set.Versions;
        }

        internal VersionedFileSet? Get(string? baseFileName)
        {
            return baseFileName is not null
                ? _sets.SingleOrDefault(_ => _.BaseFileName == baseFileName)
                : null;
        }

        internal void AddFilePath(string filePath)
        {
            var baseFileName = FileVersionHelpers.GetBaseFileName(filePath);

            var set = _sets.SingleOrDefault(_ => _.BaseFileName == baseFileName);

            if (set is null)
            {
                _sets.Add(new VersionedFileSet(baseFileName, filePath));
            }
            else
            {
                set.AddVersion(filePath);
            }
        }
    }

    internal class VersionedFileSet
    {
        private readonly string _baseFileName;
        private readonly List<string> _versions = new();

        internal VersionedFileSet(string baseFilename, string? filePath = null)
        {
            _baseFileName = baseFilename;

            if (filePath is not null)
            {
                AddVersion(filePath);
            }
        }

        internal void AddVersion(string filePath)
        {
            _versions.Add(filePath);
        }

        internal int LatestVersionNumber
        {
            get
            {
                if (_versions.Any())
                {
                    return _versions.OrderBy(_ => _).Last().ExtractVersionNumber();
                }
                else
                {
                    return 0;
                }
            }
        }

        internal string BaseFileName => _baseFileName;
        internal IEnumerable<string> Versions => _versions.OrderBy(_ => _);
    }
}
