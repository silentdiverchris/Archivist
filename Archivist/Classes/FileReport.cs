using Archivist.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Archivist.Classes
{
    /// <summary>
    /// Represents a register of all archives known to the system, where they are, how 
    /// big they are and when they were last updated.
    /// </summary>
    public class FileReport
    {
        public List<FileReportItem> Items = new();

        public void Add(FileInfo fi, bool inPrimaryArchive)
        {
            bool fuzzyMatch = false;

            // File sizes will not match when files are on disks with different allocation sizes

            var item = Items.SingleOrDefault(_ =>
                _.FileName == fi.Name &&
                _.LastWriteUtc == fi.LastWriteTimeUtc &&
                _.Length == fi.Length);

            if (item is null)
            {
                // Is it almost like one we already have ?
                // If they are last written within 1 minute then we consider them to be the same archive;

                DateTime minDate = fi.LastWriteTimeUtc.AddMinutes(-1);
                DateTime maxDate = fi.LastWriteTimeUtc.AddMinutes(1);

                item = Items.SingleOrDefault(_ =>
                    _.FileName == fi.Name &&
                    _.LastWriteUtc >= minDate &&
                    _.LastWriteUtc <= maxDate);

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
            LastWriteUtc = fi.LastWriteTimeUtc;
            LastWriteLocal = fi.LastWriteTime;

            IsVersioned = fi.IsVersionedFile(out string rootName);

            RootFileName = rootName;

            Instances = new List<FileReportItemInstance>
            {
                new FileReportItemInstance(fi, inPrimaryArchive, fuzzyMatch)
            };
        }

        public string FileName { get; set; }
        public long Length { get; set; }
        public DateTime LastWriteUtc { get; set; }
        public DateTime LastWriteLocal { get; set; }
        public List<FileReportItemInstance> Instances { get; set; }

        public string RootFileName { get; private set; }
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
            LastWriteUtc = fi.LastWriteTimeUtc;
            LastWriteLocal = fi.LastWriteTime;
            Path = fi.DirectoryName;
        }

        public string FileName { get; set; }
        public bool IsFuzzyMatch { get; set; }
        public bool IsInPrimaryArchive { get; set; }
        public string Path { get; set; }
        public long Length { get; set; }
        public DateTime LastWriteUtc { get; set; }
        public DateTime LastWriteLocal { get; set; }
    }
}
