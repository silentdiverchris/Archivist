using System;

namespace Archivist.Models
{
    internal class JobDetails
    {
        internal JobDetails(
            string jobNameToRun)
        {
            StartedUtc = DateTime.UtcNow;
            JobNameToRun = jobNameToRun;
        }

        internal string JobNameToRun { get; set; }

        internal DateTime? StartedUtc { get; set; } = null;
        internal DateTime? EndedUtc { get; set; } = null;
    }
}
