using Archivist.Utilities;

namespace Archivist.Classes
{
    public class FileReport
    {
        public List<FileReportItem> Items = new();

        public void Add(FileInfo fi, bool inPrimaryArchive)
        {
            bool fuzzyMatch = false;

            // File sizes will not match when files are on disks with different allocation sizes

            var item = Items.SingleOrDefault(_ =>
                _.Name == fi.Name &&
                _.LastWriteUtc == fi.LastWriteTimeUtc &&
                _.Length == fi.Length);

            if (item is null)
            {
                // Is it almost like one we already have ?
                // If they are within 1 minute then we consider them to be the same archive;

                DateTime minDate = fi.LastWriteTimeUtc.AddMinutes(-1);
                DateTime maxDate = fi.LastWriteTimeUtc.AddMinutes(1);

                item = Items.SingleOrDefault(_ =>
                    _.Name == fi.Name &&
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
            Name = fi.Name;
            Length = fi.Length;
            LastWriteUtc = fi.LastWriteTimeUtc;

            IsVersioned = FileUtilities.IsFileVersioned(fi.Name, out string rootName);

            RootName = rootName;

            Instances = new List<FileReportItemInstance>
            {
                new FileReportItemInstance(fi, inPrimaryArchive, fuzzyMatch)
            };
        }

        public bool IsVersioned { get; set; }
        public string Name { get; set; }
        public string RootName { get; set; }
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
