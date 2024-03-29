﻿using Archivist.Utilities;
using static Archivist.Enumerations;

namespace Archivist.Classes
{
    public class ArchiveSourceDirectory : ArchiveDirectoryBase
    {
        //private readonly SourceDirectory _sourceDirectory;
        private readonly string _baseArchiveFileName;

        internal ArchiveSourceDirectory(SourceDirectory dir) : base(enDirectoryType.Source, dir.GetBase())
        {
            //_sourceDirectory = dir;
            _baseArchiveFileName = FileUtilities.GenerateBaseOutputFileName(dir);
        }

        public string BaseFileName => _baseArchiveFileName;
    }
}
