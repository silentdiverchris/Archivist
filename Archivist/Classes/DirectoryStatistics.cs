namespace Archivist.Classes
{
    public class DirectoryStatistics
    {
        public int FilesAdded { get; private set; }
        public int FilesDeleted { get; private set; }
        public double BytesAdded { get; private set; }
        public double BytesDeleted { get; private set; }

        public int ItemsFound { get; private set; }
        public int ItemsProcessed { get; private set; }
        public long BytesProcessed { get; private set; }

        public double BytesFreeInitial = 0;
        public double BytesFreeFinal = 0;

        public void SubsumeStatistics(DirectoryStatistics stats)
        {
            FilesAdded += stats.FilesAdded;
            FilesDeleted += stats.FilesDeleted;
            BytesAdded += stats.BytesAdded;
            BytesDeleted += stats.BytesDeleted;
            ItemsFound += stats.ItemsFound;
            ItemsProcessed += stats.ItemsProcessed;
            BytesProcessed += stats.BytesProcessed;
        }

        public void FileFound(int itemCount = 1)
        {
            ItemsFound += itemCount;
        }

        public void FileDeleted(long bytesTotal, int fileCount = 1)
        {
            FilesDeleted += fileCount;
            BytesDeleted += bytesTotal;

            ItemsProcessed += fileCount;
            BytesProcessed += bytesTotal;
        }

        public void FileProcessed(long bytesTotal, int fileCount = 1)
        {
            ItemsProcessed += fileCount;
            BytesProcessed += bytesTotal;
        }

        public void FiledAdded(long bytesTotal, int fileCount = 1)
        {
            FilesAdded += fileCount;
            BytesAdded += bytesTotal;

            ItemsProcessed += fileCount;
            BytesProcessed += bytesTotal;
        }
    }
}
