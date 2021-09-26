using Archivist.Models;
using static Archivist.Enumerations;

namespace Archivist.Classes
{
    internal class ArchiveDestinationDirectory : ArchiveDirectoryBase
    {
        internal ArchiveDestinationDirectory(BaseDirectoryFiles dir) : base(enDirectoryType.Destination, dir.GetBase())
        {
        }
    }
}
