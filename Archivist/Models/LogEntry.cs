using System;
using static Archivist.Enumerations;

namespace Archivist.Models
{
    internal class LogEntry
    {
        internal LogEntry()
        {
            CreatedUtc = DateTime.UtcNow;
        }

        internal LogEntry(string logText, enSeverity severity = enSeverity.Info, DateTime? createdUtc = null, bool alwaysWriteToEventLog = false)
        {
            Text = logText;
            Severity = severity;
            CreatedUtc = createdUtc ?? DateTime.UtcNow;
            AlwaysWriteToEventLog = alwaysWriteToEventLog;
        }

        internal bool AlwaysWriteToEventLog { get; set; }
        internal DateTime CreatedUtc { get; private set; }
        internal enSeverity Severity { get; set; }
        internal string Text { get; set; }

        internal string FormatForFile()
        {
            return $"{CreatedUtc:HH:mm:ss} {Severity.ToString().PadRight(7)} {Text}\r\n";
        }
    }
}
