using System;
using static Archivist.Enumerations;

namespace Archivist.Classes
{
    internal class ArchiveAction
    {
        private readonly enArchiveActionType _type;
        private readonly ArchiveDestinationDirectory? _destinationDirectory;
        private readonly ArchiveFileInstance? _fileInstance;
        private readonly ArchiveDirectoryBase? _sourceDirectory;
        private readonly string? _primaryArchiveDirectoryPath;

        internal ArchiveAction(enArchiveActionType type, ArchiveFileInstance? fileInstance = null, ArchiveDirectoryBase? sourceDirectory = null, string? primaryArchiveDirectoryPath = null, ArchiveDestinationDirectory? destinationDirectory = null)
        {
            _type = type;

            switch (type)
            {
                case enArchiveActionType.Compress:
                    {
                        if (sourceDirectory is null)
                        {
                            throw new ArgumentException($"Action type {type} specified but no source directory supplied");
                        }

                        if (primaryArchiveDirectoryPath is null)
                        {
                            throw new ArgumentException($"Action type {type} specified but no primary directory supplied");
                        }

                        _sourceDirectory = sourceDirectory;
                        _primaryArchiveDirectoryPath = primaryArchiveDirectoryPath;
                        break;
                    }
                case enArchiveActionType.Delete:
                    {
                        if (fileInstance is null)
                        {
                            throw new ArgumentException($"Action type {type} specified but no file instance supplied");
                        }

                        _fileInstance = fileInstance;
                        break;
                    }
                case enArchiveActionType.Copy:
                    {
                        if (fileInstance is null)
                        {
                            throw new ArgumentException($"Action type {type} specified but no file instance supplied");
                        }

                        if (destinationDirectory is null)
                        {
                            throw new ArgumentException($"Action type {type} specified but no destination directory supplied");
                        }

                        _destinationDirectory = destinationDirectory;
                        break;
                    }
                default:
                    {
                        throw new ArgumentException($"ArchiveAction constructor found unsupported action type {type}");
                    }
            }

            switch (type)
            {
                case enArchiveActionType.Compress:
                    {
                        if (sourceDirectory is null)
                        {
                            throw new ArgumentException($"Action type {type} specified but no source directory supplied");
                        }

                        _sourceDirectory = sourceDirectory;
                        break;
                    }
                case enArchiveActionType.Copy:
                case enArchiveActionType.Delete:
                    {
                        if (fileInstance is null)
                        {
                            throw new ArgumentException($"Action type {type} specified but no source file supplied");
                        }

                        _fileInstance = fileInstance;
                        break;
                    }
                default:
                    {
                        throw new ArgumentException($"ArchiveAction constructor found unsupported action type {type}");
                    }
            }
        }

        internal enArchiveActionType Type => _type;
        internal ArchiveDestinationDirectory? DestinationDirectory => _destinationDirectory;
        internal ArchiveFileInstance? SourceFile => _fileInstance;
        internal ArchiveDirectoryBase? SourceDirectory => _sourceDirectory;
        internal string? PrimaryArchiveDirectoryPath => _primaryArchiveDirectoryPath;

        internal string Description
        {
            get
            {
                return _type switch
                {
                    enArchiveActionType.Compress => $"Compress {_sourceDirectory!.Path} to {_primaryArchiveDirectoryPath}",
                    enArchiveActionType.Copy => $"Copy {_fileInstance!.FullName} to {_destinationDirectory!.Path}",
                    enArchiveActionType.Delete => $"Delete {_fileInstance!.FullName}",
                    _ => $"ArchiveAction.Description found unsupported action type {_type}"
                };
            }
        }
    }
}
