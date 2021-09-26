using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archivist
{
    internal static class Enumerations
    {
        internal enum enSeverity
        {
            Debug = 0,
            Info = 1,
            Success = 2,
            Warning = 3,
            Error = 4,
        }

        // Make sure deletes are the last ones, they get exectuted in numeric order
        internal enum enArchiveActionType
        {
            Compress = 1,
            Copy = 2,
            Delete = 3
        }

        internal enum enDirectoryType
        {
            Primary = 1,
            Source = 2,
            Destination = 3
        }
    }
}
