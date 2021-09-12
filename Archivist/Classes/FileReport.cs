using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archivist.Classes
{
    public class FileReport
    {
        public List<FileReportItem> Items = new();

        public void Add(FileInfo fi, bool inPrimaryArchive)
        {
            bool fuzzyMatch = false;

            // This will not match when files are on disks with different allocation sizes, there will be a difference in the lengths
            var item = Items.SingleOrDefault(_ => 
                _.Name == fi.Name && 
                _.LastWriteUtc == fi.LastWriteTimeUtc && 
                _.Length == fi.Length);

            if (item is null)
            {
                // OK, is it almost like one we already have...
                fuzzyMatch = true;

                long minLength = fi.Length - (fi.Length / 20);
                long maxLength = fi.Length + (fi.Length / 20);
                DateTime minDate = fi.LastWriteTimeUtc.AddMinutes(-5);
                DateTime maxDate = fi.LastWriteTimeUtc.AddMinutes(5);

                item = Items.SingleOrDefault(_ => 
                    _.Name == fi.Name && 
                    _.LastWriteUtc >= minDate &&
                    _.LastWriteUtc <= maxDate &&                    
                    _.Length >= minLength &&
                    _.Length <= maxLength);
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
            Name = fi.Name;
            Length = fi.Length;
            LastWriteUtc = fi.LastWriteTimeUtc;

            Instances = new List<FileReportItemInstance>
            {
                new FileReportItemInstance(fi, inPrimaryArchive, fuzzyMatch)
            };
        }

        public string Name { get; set; }
        public long Length { get; set; }
        public DateTime LastWriteUtc { get; set; }
        public List<FileReportItemInstance> Instances { get; set; }
    }

    public class FileReportItemInstance
    {
        public FileReportItemInstance(FileInfo fi, bool inPrimaryArchive, bool fuzzyMatch)
        {
            IsFuzzyMatch = fuzzyMatch;
            IsInPrimaryArchive = inPrimaryArchive;
            Length = fi.Length;
            LastWriteUtc = fi.LastWriteTimeUtc;
            Path = fi.DirectoryName;
        }

        public bool IsFuzzyMatch { get; set; }
        public bool IsInPrimaryArchive { get; set; }
        public string Path { get; set; }
        public long Length { get; set; }
        public DateTime LastWriteUtc { get; set; }
    }
}
