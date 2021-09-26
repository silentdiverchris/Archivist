using System;
using static Archivist.Enumerations;

namespace Archivist.Classes
{
    internal class ArchiveFileAction
    {
        private readonly enArchiveActionType _type;
        private readonly ArchiveDestinationDirectory? _destinationDirectory;
        private readonly ArchiveFileInstance? _fileInstance;
        private readonly ArchiveDirectoryBase? _sourceDirectory;
        private readonly string? _primaryArchiveDirectoryPath;

        internal ArchiveFileAction(enArchiveActionType type, ArchiveFileInstance? fileInstance = null, ArchiveDirectoryBase? sourceDirectory = null, string? primaryArchiveDirectoryPath = null, ArchiveDestinationDirectory? destinationDirectory = null)
        {
            _type = type;

            switch (type)
            {
                case enArchiveActionType.CompressToPrimary:
                case enArchiveActionType.DeleteFromPrimary:
                    {
                        if (primaryArchiveDirectoryPath is null)
                        {
                            throw new ArgumentException($"Action type {type} specified but no primary directory supplied");
                        }

                        _primaryArchiveDirectoryPath = primaryArchiveDirectoryPath;
                        break;
                    }
                case enArchiveActionType.CopyToDestination:
                case enArchiveActionType.DeleteFromDestination:
                    {
                        if (destinationDirectory is null)
                        {
                            throw new ArgumentException($"Action type {type} specified but no destination directory supplied");
                        }

                        _destinationDirectory = destinationDirectory;
                        break;
                    }
                default:
                    {
                        throw new ArgumentException($"ArchiveFileAction constructor found unsupported action type {type}");
                    }
            }

            switch (type)
            {
                case enArchiveActionType.CompressToPrimary:
                    {
                        if (sourceDirectory is null)
                        {
                            throw new ArgumentException($"Action type {type} specified but no source directory supplied");
                        }

                        _sourceDirectory = sourceDirectory;
                        break;
                    }
                case enArchiveActionType.DeleteFromPrimary:
                case enArchiveActionType.CopyToDestination:
                case enArchiveActionType.DeleteFromDestination:
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
                        throw new ArgumentException($"ArchiveFileAction constructor found unsupported action type {type}");
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
                    enArchiveActionType.CompressToPrimary => $"Compress {_sourceDirectory!.Path} to {_primaryArchiveDirectoryPath}",
                    enArchiveActionType.CopyToDestination => $"Copy {_fileInstance!.FullName} to {_destinationDirectory!.Path}",
                    enArchiveActionType.DeleteFromPrimary => $"Delete {_fileInstance!.FullName}",
                    enArchiveActionType.DeleteFromDestination => $"Delete {_fileInstance!.FullName}",
                    _ => $"ArchiveFileAction.Description found unsupported action type {_type}"
                };
            }
        }
    }
}
