using System.Collections.Generic;
using System.Linq;
using static Archivist.Enumerations;

namespace Archivist.Classes
{
    /// <summary>
    /// Represents the set of files in a primary archive directory and each source and destination 
    /// directorie, and determines what actions need to happen to them.
    /// 
    /// This is regenerated at each stage, i.e. before compression, copying and deleting old archives. 
    /// 
    /// It could be updated as we go along, which would be more efficient but a lot more complicated and 
    /// thus prone to bugs and frankly too much like hard work just for the sake of being clever, especially 
    /// when nobody is paying me by the day to write this :). 
    /// 
    /// In the end we're spending minutes at a time compressing and copying gigabytes of archive data all over 
    /// the place; saving a fraction of a second of CPU time along the way really isn't worth the effort, so 
    /// we just regenerate it before each stage, nice and simple.
    /// </summary>

    public class ArchiveRegister
    {
        private readonly ArchivePrimaryDirectory _primary;
        private List<ArchiveDestinationDirectory> _destinations { get; set; } = new();
        private List<ArchiveSourceDirectory> _sources { get; set; } = new();

        public ArchiveRegister(string primaryDirectoryPath, List<Models.SourceDirectory> sources, List<Models.ArchiveDirectory> destinations)
        {
            _primary = new(primaryDirectoryPath);

            foreach (var src in sources)
            {
                AddSourceDirectory(src);
            }

            foreach (var dst in destinations)
            {
                AddDestinationDirectory(dst);
            }

            // The constructors above have already populated the internal lists of existing files

            // Determine which files in the primary need to be copied to which destinations
            // Determine which files in the primary need to be deleted
            // Determine which files in the destinations need to be deleted
            AssignActions();
        }

        internal IEnumerable<ArchiveFileAction> Actions
        {
            get
            {
                List<ArchiveFileAction> actions = new();
                
                var priActions = _primary.Files.Where(_ => _.Actions.Any()).SelectMany(_ => _.Actions);
                actions.AddRange(priActions);
                
                foreach (var dst in _destinations)
                {
                    var dstActions = dst.Files.Where(_ => _.Actions.Any()).SelectMany(_ => _.Actions);
                    actions.AddRange(dstActions);
                }

                return actions;
            }
        }

        /// <summary>
        /// Determine what needs to be done to which files, currently this just handles the file copying. 
        /// Extend this to the deleting and compresisng actions too.
        /// </summary>
        private void AssignActions()
        {
            // Versioned files, only copy those versions that are more recent than
            // those which already exist in the destination

            foreach (var priArcFil in _primary.VersionedFileInstances)
            {
                foreach (var dstArchive in _destinations.Where(_ => _.IsEnabledAndAvailable))
                {
                    if (dstArchive.WantsFile(priArcFil))
                    {
                        var dstVerSet = dstArchive.VersionedFileSets.Get(priArcFil.BaseFileName);

                        if (dstVerSet is null || priArcFil.VersionNumber > dstVerSet.LatestVersionNumber)
                        {
                            if (dstArchive.IsAbsent(priArcFil.FileName))
                            {
                                priArcFil.AddAction(new ArchiveFileAction(enArchiveActionType.CopyToDestination, fileInstance: priArcFil, destinationDirectory: dstArchive));
                            }
                        }
                    }
                }
            }

            // Unversioned files, copy these over if they are absent
            // or stale in the destination

            foreach (var priArcFil in _primary.UnversionedFileInstances)
            {
                foreach (var dstArchive in _destinations.Where(_ => _.IsEnabledAndAvailable))
                {
                    if (dstArchive.WantsFile(priArcFil))
                    {
                        if (dstArchive.IsAbsentOrStale(priArcFil.FileName, priArcFil.LastWriteTimeLocal))
                        {
                            priArcFil.AddAction(new ArchiveFileAction(enArchiveActionType.CopyToDestination, fileInstance: priArcFil, destinationDirectory: dstArchive));
                        }
                    }
                }
            }
        }

        private void AddDestinationDirectory(Models.ArchiveDirectory directory)
        {
            var dir = directory.GetBase();

            _destinations.Add(new ArchiveDestinationDirectory(dir));
        }

        private void AddSourceDirectory(Models.SourceDirectory directory)
        {
            _sources.Add(new ArchiveSourceDirectory(directory));
        }

        
    }
}
