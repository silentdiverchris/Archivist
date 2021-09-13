namespace Archivist.Models
{
    internal class JobDetails
    {
        internal JobDetails(
            string jobNameToRun, 
            AppSettings appSettings)
        {
            StartedUtc = DateTime.UtcNow;

            JobNameToRun = jobNameToRun;
            AppSettings = appSettings;
        }

        internal string JobNameToRun { get; set; }
        internal AppSettings AppSettings { get; set; }

        internal DateTime? StartedUtc { get; set; } = null;
        internal DateTime? EndedUtc { get; set; } = null;

        internal string SqlConnectionString => AppSettings?.SqlConnectionString;
        internal string LogDirectoryPath => AppSettings?.LogDirectoryPath;
        
        internal bool WriteToConsole => AppSettings?.SelectedJob?.WriteToConsole ?? true;

        internal Job SelectedJob => AppSettings?.SelectedJob;
    }
}
