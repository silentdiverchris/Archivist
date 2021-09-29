using Archivist.Classes;
using System.Text.Json.Serialization;

namespace Archivist.Classes
{
    /// <summary>
    /// Directories for archived files to be copied to from 
    /// the primary archive directory
    /// </summary>
    public class ArchiveDirectory : BaseDirectoryFiles
    {
        [JsonIgnore]
        public DirectoryStatistics Statistics { get; set; } = new();
    }
}