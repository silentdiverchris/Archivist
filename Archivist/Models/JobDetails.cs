using System;

namespace Archivist.Models
{
    internal class JobDetails
    {
        internal JobDetails(
            string jobName, 
            string configFilePath, 
            string sqlConnectionString,
            string logDirectoryName,
            string aesEncryptExecutable,
            bool debugConsole)
        {
            StartedUtc = DateTime.UtcNow;

            JobName = jobName;
            ConfigFilePath = configFilePath;
            SqlConnectionString = sqlConnectionString;
            LogDirectoryName = logDirectoryName;
            AesEncryptExecutable = aesEncryptExecutable;
            DebugConsole = debugConsole;
        }

        internal string JobName { get; set; }
        internal string ConfigFilePath { get; set; }

        internal DateTime? StartedUtc { get; set; } = null;
        internal DateTime? EndedUtc { get; set; } = null;

        internal string SqlConnectionString { get; set; }
        internal string LogDirectoryName { get; set; }
        internal string AesEncryptExecutable { get; set; }
        internal bool DebugConsole { get; set; }
    }
}
