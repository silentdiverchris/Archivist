namespace Archivist.Helpers
{
    internal static class FileHelpers
    {
        internal static string GetByteSizeAsText(double sizeBytes)
        {
            if (sizeBytes > (1024 * 1024 * 1024))
            {
                return $"{(sizeBytes / 1024 / 1024 / 1024):N2} GB";
            }
            else if(sizeBytes > (1024 * 1024))
            {
                return $"{(sizeBytes / 1024 / 1024):N2} MB";
            }
            else if (sizeBytes > 1024)
            {
                return $"{(sizeBytes / 1024):N2} KB";
            }
            else
            {
                return $"{sizeBytes:N0} bytes";
            }
        }
    }
}
