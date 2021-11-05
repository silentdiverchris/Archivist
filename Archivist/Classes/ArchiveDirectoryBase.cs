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
        private readonly BaseDirectoryFiles _baseDirectory;
        private readonly VersionedFileSets _versionedFileSets = new();

        private readonly List<ArchiveFileInstance> _existingFiles = new();
        private readonly List<Regex> _includeRegexList = new();
        private readonly List<Regex> _excludeRegexList = new();

        internal bool IsAvailable => _baseDirectory.IsAvailable;
        internal bool IsEnabledAndAvailable => _baseDirectory.IsAvailable && _baseDirectory.IsEnabled;

        internal BaseDirectoryFiles? BaseDirectory => _baseDirectory;

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

                        if (!Directory.Exists(path))
                        {
                            throw new ArgumentException($"DirectoryBase constructor for type {type} given invalid path '{path}'");
                        }

                        _baseDirectory = new BaseDirectoryFiles()
                        {
                            DirectoryPath = path
                        };

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

            if (IsEnabledAndAvailable)
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
                    throw new Exception($"DirectoryBase.Initialise found null source or destination directory");
                }
            }

            if (IsEnabledAndAvailable)
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

                var filePathList = Directory.GetFiles(_baseDirectory.DirectoryPath!, "*.*", searchOption);

                foreach (var filePath in filePathList)
                {
                    Result result = new($"ArchiveFileInstance {filePath}");

                    bool include = true;

                    // Source and destination directories can have inclusion and exclusion file specifications, honour them...

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
                            throw new Exception($"DirectoryBase.Initialise found source or destination directory with no base directory");
                        }
                    }

                    _existingFiles.Add(new ArchiveFileInstance(filePath: filePath, directory: _baseDirectory, ignored: !include, isLatestVersion: false, result));
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

                // Mark latest versions, this was a late addition so is a bit hamfisted right now, nothing prevents two
                // versions, or no versions being the latest other than the code below, which determines which to mark TODO

                foreach (string baseFileName in _versionedFileSets.BaseFileNames)
                {
                    var set = _versionedFileSets.Get(baseFileName);

                    if (set is not null)
                    {
                        string latestFileName = set.Versions.OrderBy(_ => _).Last();

                        var latestInstances = _existingFiles.Where(_ => _.FullName.EndsWith(latestFileName));

                        if (latestInstances.Any())
                        {
                            foreach (var filInst in latestInstances)
                            {
                                filInst.SetIsLatestVersion();
                            }
                        }
                        else
                        {
                            throw new Exception($"ArchiveDirectoryBase.Initialise failed to find latest instances of {latestFileName}");
                        }
                    }
                }
            }
        }

        internal string Path => _baseDirectory.DirectoryPath!;

        internal IEnumerable<ArchiveFileInstance> AllFiles => _existingFiles.OrderBy(_ => _.BaseFileName);
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
                    filInst.Result.AddInfo($"Destination '{_baseDirectory.DirectoryPath}' wants include spec '{spec}'");
                    break;
                }
            }

            if (matchesIncludes == false)
            {
                filInst.Result.AddInfo($"Destination '{_baseDirectory.DirectoryPath}' found no matching include spec");
            }
            else
            {
                foreach (var spec in _excludeRegexList)
                {
                    if (spec.IsMatch(filInst.FileName))
                    {
                        matchesExcludes = true;
                        filInst.Result.AddInfo($"Destination '{_baseDirectory.DirectoryPath}' doesn't want exclude spec '{spec}'");
                        break;
                    }
                }
            }

            return matchesIncludes && !matchesExcludes;
        }
    }
}
