using Archivist.Models;
using Archivist.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static Archivist.Enumerations;

namespace Archivist.Classes
{
    internal class ArchiveDirectoryBase
    {
        private readonly enDirectoryType _type;
        private readonly BaseDirectoryFiles? _baseDirectory;
        private readonly string _path;
        private readonly bool _isAvailable;
        private readonly List<ArchiveFileInstance> _existingFiles = new();
        private readonly VersionedFileSets _versionedFileSets = new();

        private List<Regex> _includeRegexList = new();
        private List<Regex> _excludeRegexList = new();

        internal bool IsEnabled
        {
            get
            {
                bool isEnabled = _type switch
                {
                    enDirectoryType.Primary => true,
                    enDirectoryType.Source => _baseDirectory!.IsEnabled && _baseDirectory.IsAvailable,
                    enDirectoryType.Destination => _baseDirectory!.IsEnabled && _baseDirectory.IsAvailable,
                    _ => throw new Exception($"DirectoryBase.Initialise found unsupported type {_type}")
                };

                return isEnabled;
            }
        }

        internal bool IsAvailable => _isAvailable;
        internal bool IsEnabledAndAvailable => _isAvailable && IsEnabled;

        internal ArchiveDirectoryBase(enDirectoryType type, BaseDirectoryFiles? bdf = null, string? path = null)
        {
            _type = type;

            switch (_type)
            {
                case enDirectoryType.Primary:
                    {
                        if (bdf is not null)
                        {
                            throw new ArgumentException($"DirectoryBase constructor for type {type} given non-null value for bdf");
                        }

                        if (path is null)
                        {
                            throw new Exception($"DirectoryBase constructor for type {type} for null path");
                        }

                        _path = path!;

                        if (Directory.Exists(_path))
                        {
                            _isAvailable = true;
                        }
                        else
                        {
                            throw new ArgumentException($"DirectoryBase constructor for type {type} given non-existant path '{_path}'");
                        }

                        break;
                    }
                case enDirectoryType.Source:
                case enDirectoryType.Destination:
                    {
                        if (path is not null)
                        {
                            throw new ArgumentException($"DirectoryBase constructor for type {type} given non-null value for path");
                        }

                        if (bdf is not null)
                        {
                            _baseDirectory = bdf;
                            _isAvailable = bdf.IsAvailable;

                            _path = bdf.DirectoryPath!;
                        }
                        else
                        {
                            throw new ArgumentException($"DirectoryBase constructor for type {type} given null value for bdf");
                        }

                        break;
                    }
                default:
                    {
                        throw new Exception($"DirectoryBase constructor got unsupported type {type}");
                    }
            }

            if (_isAvailable)
            {
                Initialise();
            }
        }

        private void Initialise()
        {
            // Source and destination directories have inclusion and exclusion file
            // specifications, prepare regexes to match file names against them

            short sourcePriority = 99;

            if (_type == enDirectoryType.Source || _type == enDirectoryType.Destination)
            {

                if (_baseDirectory is not null)
                {
                    sourcePriority = _baseDirectory.Priority;

                    if (_baseDirectory.IncludeSpecifications is not null)
                    {
                        foreach (var includeSpec in _baseDirectory.IncludeSpecifications)
                        {
                            _includeRegexList.Add(includeSpec.GenerateRegexForFileMask());
                        }
                    }

                    if (_baseDirectory.ExcludeSpecifications is not null)
                    {
                        foreach (var excludeSpec in _baseDirectory.ExcludeSpecifications)
                        {
                            _excludeRegexList.Add(excludeSpec.GenerateRegexForFileMask());
                        }
                    }
                }
                else
                {
                    throw new Exception($"DirectoryBase.Initialise found source or destination directory {_path} with no base directory");
                }
            }

            if (IsEnabled)
            {
                // Don't use any file specification here, take them all, we want to catalogue all the files 
                // even though we might then ignore some of them for whatever reason.

                // For a source directory we would in theory want the entire content as deep as it goes but currently we
                // Don't use the file names found in source directories, so let's not recurse into there for the moment

                SearchOption searchOption = _type switch
                {
                    enDirectoryType.Primary => SearchOption.TopDirectoryOnly,
                    enDirectoryType.Source => SearchOption.TopDirectoryOnly, //.AllDirectories, 
                    enDirectoryType.Destination => SearchOption.TopDirectoryOnly,
                    _ => throw new Exception($"DirectoryBaswe.Initialise found unsupported type {_type}")
                };

                var filePathList = Directory.GetFiles(_path, "*.*", searchOption);

                foreach (var filePath in filePathList)
                {
                    Result result = new($"ArchiveFileInstance {filePath}");

                    bool include = true;

                    // Source and destination directories have inclusion and exclusion file specifications, honour them...

                    if (_type == enDirectoryType.Source || _type == enDirectoryType.Destination)
                    {
                        if (_baseDirectory is not null)
                        {
                            bool matchesIncludes = true;
                            bool matchesExcludes = false;

                            foreach (var spec in _includeRegexList)
                            {
                                if (spec.IsMatch(filePath))
                                {
                                    matchesIncludes = true;
                                    result.AddInfo($"Matches include spec '{spec}'");
                                    break;
                                }
                            }

                            foreach (var spec in _excludeRegexList)
                            {
                                if (spec.IsMatch(filePath))
                                {
                                    matchesExcludes = true;
                                    result.AddInfo($"Matches exclude spec '{spec}'");
                                    break;
                                }
                            }

                            include = matchesIncludes && !matchesExcludes;
                        }
                        else
                        {
                            throw new Exception($"DirectoryBase.Initialise found source or destination directory {_path} with no base directory");
                        }
                    }

                    _existingFiles.Add(new ArchiveFileInstance(filePath: filePath, ignored: !include, isLatestVersion: false, sourcePriority: sourcePriority, result));
                }

                // Build a representation of the versioned file sets for primary and destination
                // directories, source directories don't have versioned files

                if (_type == enDirectoryType.Primary || _type == enDirectoryType.Destination)
                {
                    foreach (var fileInst in VersionedFileInstances.OrderBy(_ => _.FileName))
                    {
                        _versionedFileSets.AddFilePath(fileInst.FileName);
                    }
                }

                // Mark latest versions, it'd be nice to do this on the fly TODO

                foreach (string baseFileName in _versionedFileSets.BaseFileNames)
                {
                    var set = _versionedFileSets.Get(baseFileName);

                    if (set is not null)
                    {
                        string latestFileName = set.Versions.OrderBy(_ => _).Last();
                        
                        var filInst = _existingFiles.SingleOrDefault(_ => _.FullName.EndsWith(latestFileName));

                        if (filInst is not null)
                        {
                            filInst.SetIsLatestVersion();
                        }
                        else
                        {
                            throw new Exception($"ArchiveDirectoryBase.Initialise failed to find latest instance {latestFileName}");
                        }
                    }
                }
            }
        }

        internal string Path => _path;

        internal IEnumerable<ArchiveFileInstance> AllFiles => _existingFiles;
        internal IEnumerable<ArchiveFileInstance> Files => _existingFiles.Where(_ => _.Ignored == false);
        internal IEnumerable<ArchiveFileInstance> IgnoredFiles => _existingFiles.Where(_ => _.Ignored == true);
        internal IEnumerable<ArchiveFileInstance> VersionedFileInstances => Files.Where(_ => _.IsVersioned == true);
        internal IEnumerable<ArchiveFileInstance> UnversionedFileInstances => Files.Where(_ => _.IsVersioned == false);

        internal VersionedFileSets VersionedFileSets => _versionedFileSets;

        /// <summary>
        /// Do we have a file of this name at or very close to this lastWriteTime ?
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="lastWriteTimeLocal"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        internal bool HasUpToDateCopy(string fileName, DateTime lastWriteTimeLocal)
        {
            var existing = _existingFiles.SingleOrDefault(_ => _.FileName == fileName);

            if (existing is not null)
            {
                var howStale = existing.LastWriteTimeLocal - lastWriteTimeLocal;

                return howStale.TotalSeconds < Constants.STALE_SECONDS_THRESHOLD;
            }
            else
            {
                return false;
            }
        }

        internal bool IsAbsent(string fileName)
        {
            return _existingFiles.SingleOrDefault(_ => _.FileName == fileName) == null;
        }

        internal bool IsAbsentOrStale(string fileName, DateTime lastWriteTimeLocal)
        {
            return !HasUpToDateCopy(fileName, lastWriteTimeLocal);
        }

        internal bool WantsFile(ArchiveFileInstance filInst)
        {
            bool matchesIncludes = _includeRegexList.Any() ? false : true;
            bool matchesExcludes = false;

            foreach (var spec in _includeRegexList)
            {
                if (spec.IsMatch(filInst.FileName))
                {
                    matchesIncludes = true;
                    filInst.Result.AddInfo($"Destination '{_path}' wants include spec '{spec}'");
                    break;
                }
            }

            if (matchesIncludes == false)
            {
                filInst.Result.AddInfo($"Destination '{_path}' found no matching include spec");
            }
            else
            {
                foreach (var spec in _excludeRegexList)
                {
                    if (spec.IsMatch(filInst.FileName))
                    {
                        matchesExcludes = true;
                        filInst.Result.AddInfo($"Destination '{_path}' doesn't want exclude spec '{spec}'");
                        break;
                    }
                }
            }

            return matchesIncludes && !matchesExcludes;
        }

    }

    internal class ArchivePrimaryDirectory : ArchiveDirectoryBase
    {
        internal ArchivePrimaryDirectory(string path) : base(enDirectoryType.Primary, path: path)
        {
        }
    }

    internal class ArchiveSourceDirectory : ArchiveDirectoryBase
    {
        private readonly Models.SourceDirectory _sourceDirectory;
        private readonly string _baseArchiveFileName;

        internal ArchiveSourceDirectory(Models.SourceDirectory dir) : base(enDirectoryType.Source, dir.GetBase())
        {
            _sourceDirectory = dir;
            _baseArchiveFileName = FileUtilities.GenerateBaseOutputFileName(dir);
        }

        public string BaseFileName => _baseArchiveFileName;
    }

    internal class ArchiveDestinationDirectory : ArchiveDirectoryBase
    {
        internal ArchiveDestinationDirectory(BaseDirectoryFiles dir) : base(enDirectoryType.Destination, dir.GetBase())
        {
        }
    }
}
