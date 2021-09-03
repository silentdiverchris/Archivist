namespace Archivist
{
    internal class Constants
    {
        internal const int STREAM_BUFFER_SIZE = 81920;

        /// <summary>
        /// The nimimum number of versions it is possible to retain
        /// </summary>
        internal const int RETAIN_VERSIONS_MINIMUM = 1;

        /// <summary>
        /// Minimum number of days to retain archives, regardless of versioning. 
        /// This overrides the number of versions to retain.
        /// </summary>
        internal const int RETAIN_YOUNGER_THAN_DAYS_MINIMUM = 1;

        internal const int DB_TIMEOUT_SECONDS = 10;

        internal const string DATE_FORMAT_DATE_TIME_LONG_SECONDS = "d MMM yyyy HH:mm:ss";
        internal const string DATE_FORMAT_DATE_TIME_YYYYMMDDHHMMSS = "yyyyMMddHHmmss";
    }
}
