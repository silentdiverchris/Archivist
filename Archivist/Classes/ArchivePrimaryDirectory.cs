using static Archivist.Enumerations;

namespace Archivist.Classes
{
    internal class ArchivePrimaryDirectory : ArchiveDirectoryBase
    {
        internal ArchivePrimaryDirectory(string path) : base(enDirectoryType.Primary, path: path)
        {
        }
    }
}
