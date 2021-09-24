namespace Archivist
{
    internal class Constants
    {
        internal const int STREAM_BUFFER_SIZE = 81920;

        /// <summary>
        /// The mimimum number of versions it is possible to retain
        /// </summary>
        internal const int RETAIN_VERSIONS_MINIMUM = 1;

        internal const int RETAIN_DAYS_OLD_MINIMUM = 1;

        internal const int DB_TIMEOUT_SECONDS = 10;

        internal const string DATE_FORMAT_DATE_TIME_LONG_SECONDS = "d MMM yyyy HH:mm:ss";
        internal const string DATE_FORMAT_DATE_TIME_LONG_SECONDS_FIXED_WIDTH = "dd MMM yyyy HH:mm:ss";
        internal const string DATE_FORMAT_DATE_TIME_YYYYMMDDHHMMSS = "yyyyMMddHHmmss";
    }
}
