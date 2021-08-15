namespace Archivist
{
    class Constants
    {
        internal const int STREAM_BUFFER_SIZE = 81920;

        /// <summary>
        /// The nimimum number of versions to retain
        /// </summary>
        internal const int RETAIN_VERSIONS_MINIMUM = 2;

        /// <summary>
        /// Minimum number of days to retain archives, regardless of versioning. 
        /// This overrides the number of versions to retain.
        /// </summary>
        internal const int RETAIN_DAYS_OLD_MINIMUM = 7;

        internal const int DB_TIMEOUT_SECONDS = 10;

        internal const string DATE_FORMAT_DATE_TIME_LONG_SECONDS = "d MMM yyyy HH:mm:ss";
        internal const string DATE_FORMAT_DATE_TIME_YYYYMMDDHHMMSS = "yyyyMMddHHmmss";
    }
}
