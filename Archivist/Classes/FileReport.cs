using Archivist.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Archivist.Classes
{
    /// <summary>
    /// Represents a register of all archives known to the system, which includes files in archive directories
    /// not created by this system. It stores where they are, how big they are and when they were last updated.
    /// </summary>
    public class FileReport
    {
        public List<FileReportItem> Items = new();

        public void Add(FileInfo fi, bool inPrimaryArchive)
        {
            bool fuzzyMatch = false;

            // File sizes will not match when files with identical content are stored on
            // disks with different allocation sizes but since most people use the default
            // then usually they will match, so let's try an exact match first.

            var item = Items.SingleOrDefault(_ =>
                _.FileName == fi.Name &&
                _.LastWriteTimeLocal == fi.LastWriteTime &&
                _.Length == fi.Length);

            if (item is null)
            {
                // No exact match, is it almost like one we already have ? - we can't completely trust the file
                // size due so if they have the same name and are last written within 2 seconds either side, then
                // we consider them to be the same archive

                // The last write fuzzy match shouldn't be necessary with all new archives (as per 5th Oct) because
                // new archives should have tiestamps rounded to the nearest second, no no loss of accuracy when
                // copied from NTFS to exFAT for example, remove this when proven... TODO

                DateTime minDate = fi.LastWriteTime.AddSeconds(-2);
                DateTime maxDate = fi.LastWriteTime.AddSeconds(2);

                item = Items.SingleOrDefault(_ =>
                    _.FileName == fi.Name &&
                    _.LastWriteTimeLocal >= minDate &&
                    _.LastWriteTimeLocal <= maxDate);

                fuzzyMatch = true;
            }

            if (item is null)
            {
                Items.Add(new FileReportItem(fi, inPrimaryArchive, false));
            }
            else
            {
                item.Instances.Add(new FileReportItemInstance(fi, inPrimaryArchive, fuzzyMatch));
            }
        }
    }

    public class FileReportItem
    {
        public FileReportItem(FileInfo fi, bool inPrimaryArchive, bool fuzzyMatch)
        {
            FileName = fi.Name;
            Length = fi.Length;
            LastWriteTimeLocal = fi.LastWriteTime;

            IsVersioned = fi.IsVersionedFile();
            BaseFileName = FileVersionHelpers.GetBaseFileName(fi.Name);

            Instances = new List<FileReportItemInstance>
            {
                new FileReportItemInstance(fi, inPrimaryArchive, fuzzyMatch)
            };
        }

        public string FileName { get; set; }
        public long Length { get; set; }
        public DateTime LastWriteTimeLocal { get; set; }
        public List<FileReportItemInstance> Instances { get; set; }

        public string BaseFileName { get; private set; }
        public bool IsVersioned { get; private set; }
    }

    public class FileReportItemInstance
    {
        public FileReportItemInstance(FileInfo fi, bool inPrimaryArchive, bool fuzzyMatch)
        {
            FileName = fi.Name;
            IsFuzzyMatch = fuzzyMatch;
            IsInPrimaryArchive = inPrimaryArchive;
            Length = fi.Length;
            LastWriteLocal = fi.LastWriteTime;
            Path = fi.DirectoryName!;
        }

        public string FileName { get; set; }
        public bool IsFuzzyMatch { get; set; }
        public bool IsInPrimaryArchive { get; set; }
        public string Path { get; set; }
        public long Length { get; set; }
        public DateTime LastWriteLocal { get; set; }
    }
}
