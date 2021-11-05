using System.Collections.Generic;
using System.Linq;
using static Archivist.Enumerations;

namespace Archivist.Classes
{
    /// <summary>
    /// Represents the set of files in a primary archive directory and each source and destination 
    /// directory, and determines what actions need to happen to them.
    /// 
    /// This is regenerated at each stage, i.e. before compression, copying and deleting old archives. 
    /// 
    /// It could be updated as we go along, which would be more efficient but more complicated, 
    /// prone to bugs and frankly too much like hard work just for the sake of being clever, especially 
    /// when nobody is paying me by the day to write this :). 
    /// 
    /// In the end we're spending minutes at a time compressing and copying gigabytes of archive data all over 
    /// the place. Saving a fraction of a second of CPU time along the way really isn't worth the effort, so 
    /// we just regenerate it before each stage, nice and simple.
    /// </summary>

    public class ArchiveRegister
    {
        private readonly Job _job;
        private readonly ArchivePrimaryDirectory _primary;

        private readonly List<ArchiveAction> _actions = new();
        private readonly List<ArchiveSourceDirectory> _sources = new();
        private readonly List<ArchiveDestinationDirectory> _destinations = new();

        public ArchiveRegister(Job jobSpec, string primaryDirectoryPath, List<SourceDirectory> sources, List<ArchiveDirectory> destinations)
        {
            _job = jobSpec;
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

        internal IEnumerable<ArchiveAction> Actions => _actions.OrderBy(_ => (int)_.Type);

        internal void AddAction(ArchiveAction action)
        {
            _actions.Add(action);
        }

        /// <summary>
        /// Determine what needs to be done to which files, currently this just handles the file copying
        /// and deleting. 
        /// Extend this to add compressification actions too.
        /// </summary>
        private void AssignActions()
        {
            // Determine source folders to be compressed

            var activeSources = _sources
                .Where(_ => _.IsEnabledAndAvailable)
                .Where(_ => _.BaseDirectory!.IsToBeProcessed(_job));

            // Determine source directories that need to be compressed into archives in the primary archive directory
            foreach (var srcDir in activeSources)
            {
                // TODO implement this at some point...
            }

            // Determine files to be copied from the primary archive directory to other archive directories

            // Versioned files first, only copy those versions that are more recent than
            // those which already exist in the destination

            foreach (var priArcFil in _primary.VersionedFileInstances.OrderBy(_ => _.FileName)) // Should already be in this order, if so, no harm done
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
                                AddAction(new ArchiveAction(enArchiveActionType.Copy, fileInstance: priArcFil, destinationDirectory: dstArchive));
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
                            AddAction(new ArchiveAction(enArchiveActionType.Copy, fileInstance: priArcFil, destinationDirectory: dstArchive));
                        }
                    }
                }
            }

            // Determine which versioned files need to be deleted in the primary archive directory, we don't ever
            // delete non-versioned files, we didn't create them so we're not going to delete them, just copy them around.

            foreach (string baseFileName in _primary.VersionedFileSets.BaseFileNames)
            {
                var versions = _primary.VersionedFileSets.VersionsOfFile(baseFileName);

                // TODO need to get these from the source directories themselves
                var retainMaximumVersions = 2;
                var retainYoungerThanDays = 2;

                int removeCount = versions.Count() - retainMaximumVersions;

                if (removeCount > 0)
                {
                    var versionsToDelete = versions.OrderBy(_ => _).Take(removeCount);

                    foreach (var fileName in versionsToDelete)
                    {
                        var filInst = _primary.AllFiles.Single(_ => _.FileName == fileName);

                        if (filInst.IsOlderThanDays(retainYoungerThanDays))
                        {
                            AddAction(new ArchiveAction(enArchiveActionType.Delete, fileInstance: filInst));
                        }
                    }
                }
            }

            // Determine which versioned files need to be deleted in the destination directories, we don't ever
            // delete non-versioned files, we didn't create them, we just copy them around.

            foreach (var dstArchive in _destinations.Where(_ => _.IsEnabledAndAvailable))
            {
                foreach (string baseFileName in dstArchive.VersionedFileSets.BaseFileNames)
                {
                    var versions = dstArchive.VersionedFileSets.VersionsOfFile(baseFileName);

                    int removeCount = versions.Count() - dstArchive.BaseDirectory!.RetainMaximumVersions;

                    if (removeCount > 0)
                    { 
                        var versionsToDelete = versions.OrderBy(_ => _).Take(removeCount);

                        foreach (var fileName in versionsToDelete)
                        {
                            var filInst = dstArchive.AllFiles.Single(_ => _.FileName == fileName);

                            if (filInst.IsOlderThanDays(dstArchive.BaseDirectory.RetainYoungerThanDays))
                            {
                                AddAction(new ArchiveAction(enArchiveActionType.Delete, fileInstance: filInst));                            
                            }
                        }
                    }
                }
            }
        }

        private void AddDestinationDirectory(ArchiveDirectory directory)
        {
            var dir = directory.GetBase();

            _destinations.Add(new ArchiveDestinationDirectory(dir));
        }

        private void AddSourceDirectory(SourceDirectory directory)
        {
            _sources.Add(new ArchiveSourceDirectory(directory));
        }
    }
}
