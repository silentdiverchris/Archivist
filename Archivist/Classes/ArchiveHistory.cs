using System;
using System.Collections.Generic;

namespace Archivist.Classes
{
    /// <summary>
    /// Considering retaining a long-term history of the actions taken to a file... maybe..
    /// </summary>
    public class ArchiveHistory
    {
        public List<FileInstance> Files = new();
    }

    public class FileInstance
    {
        public string? FullName { get; set; }

        public List<BackupInstance> Backups = new();
    }

    public class BackupInstance
    {
        public long Length { get; set; }
        public DateTime CreatedLocal { get; set; }
        public DateTime DeletedLocal { get; set; }
    }
}
