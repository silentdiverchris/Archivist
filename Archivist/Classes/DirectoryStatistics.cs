namespace Archivist.Classes
{
    public class DirectoryStatistics
    {
        public int FilesAdded = 0;
        public int FilesDeleted = 0;
        public double BytesAdded = 0;
        public double BytesDeleted = 0;

        public int ItemsFound { get; set; }
        public int ItemsProcessed { get; set; }
        public long BytesProcessed { get; set; }

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
    }
}
