using static Archivist.Enumerations;

namespace Archivist.Classes
{
    public class ArchiveDestinationDirectory : ArchiveDirectoryBase
    {
        internal ArchiveDestinationDirectory(BaseDirectoryFiles dir) : base(enDirectoryType.Destination, dir.GetBase())
        {
        }
    }
}
