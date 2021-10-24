using System;

namespace Archivist.Classes
{
    internal class JobDetails
    {
        internal JobDetails(
            string jobNameToRun)
        {
            StartTime = DateTime.Now;
            JobNameToRun = jobNameToRun;
        }

        internal string JobNameToRun { get; set; }

        internal DateTime? StartTime { get; set; } = null;
        internal DateTime? EndTime { get; set; } = null;
    }
}
