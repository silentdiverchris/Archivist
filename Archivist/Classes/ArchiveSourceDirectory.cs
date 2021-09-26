using Archivist.Utilities;
using static Archivist.Enumerations;

namespace Archivist.Classes
{
    internal class ArchiveSourceDirectory : ArchiveDirectoryBase
    {
        private readonly Models.SourceDirectory _sourceDirectory;
        private readonly string _baseArchiveFileName;

        internal ArchiveSourceDirectory(Models.SourceDirectory dir) : base(enDirectoryType.Source, dir.GetBase())
        {
            _sourceDirectory = dir;
            _baseArchiveFileName = FileUtilities.GenerateBaseOutputFileName(dir);
        }

        public string BaseFileName => _baseArchiveFileName;
    }
}
