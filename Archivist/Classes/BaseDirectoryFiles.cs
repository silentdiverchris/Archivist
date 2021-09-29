using Archivist.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Archivist.Classes
{
    /// <summary>
    /// Properies that relate to archiving and copying files
    /// </summary>
    public class BaseDirectoryFiles : BaseDirectory
    {
        public BaseDirectoryFiles GetBase()
        {
            return this;
        }

        /// <summary>
        /// Retain a maximum of this many versions in this directory, zero means we keep all versions, which
        /// will eventually fill the volume. One gotcha is that if you set this lower on an archive directory
        /// than on the source directory the system will keep copying over older versions and then deleting 
        /// them, watch out for that.
        /// </summary>
        public int RetainMaximumVersions { get; set; } = Constants.RETAIN_VERSIONS_MINIMUM;

        /// <summary>
        /// Retain files that were written less than this many days ago, this overrides
        /// the RetainMaximumVersions setting. Zero disables this function.
        /// </summary>
        public int RetainYoungerThanDays { get; set; } = Constants.RETAIN_DAYS_OLD_MINIMUM;

        /// <summary>
        /// A list of file specs to include, eg "*.txt', 'thing.*', abc???de.jpg' etc.
        /// An empty list includes all files
        /// </summary>
        public List<string> IncludeSpecifications { get; set; } = new();

        [JsonIgnore]
        public string IncludeSpecificationsText => IncludeSpecifications.Any()
            ? IncludeSpecifications.ConcatenateToDelimitedList()
            : "all files";

        /// <summary>
        /// A list of file specs to ignore, eg "*.txt', 'thing.*', abc???de.jpg' etc. 
        /// An empty list doesn't exclude any files
        /// </summary>
        public List<string> ExcludeSpecifications { get; set; } = new();

        [JsonIgnore]
        public string ExcludeSpecificationsText => ExcludeSpecifications.Any()
            ? ExcludeSpecifications.ConcatenateToDelimitedList()
            : "nothing";
    }
}