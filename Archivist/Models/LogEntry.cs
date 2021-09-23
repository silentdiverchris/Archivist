using System;
using static Archivist.Enumerations;

namespace Archivist.Models
{
    internal class LogEntry
    {
        internal LogEntry(short percentComplete, string suffix, string prefix)
        {
            ProgressPrefix = prefix;
            ProgressSuffix = suffix;
            PercentComplete = percentComplete;
        }

        internal LogEntry(string logText, enSeverity severity = enSeverity.Info, bool alwaysWriteToEventLog = false, bool consoleBlankLineBefore = false, bool consoleBlankLineAfter = false)
        {
            Text = logText;
            Severity = severity;
            AlwaysWriteToEventLog = alwaysWriteToEventLog;
            ConsoleBlankLineBefore = consoleBlankLineBefore;
            ConsoleBlankLineAfter = consoleBlankLineAfter;
        }

        internal string? ProgressPrefix { get; set; } = null;
        internal string? ProgressSuffix { get; set; } = null;
        internal short? PercentComplete { get; set; } = null;

        internal bool AlwaysWriteToEventLog { get; set; }
        internal DateTime CreatedUtc { get; private set; } = DateTime.UtcNow;
        internal DateTime CreatedLocal { get; private set; } = DateTime.Now;
        internal enSeverity Severity { get; set; }
        internal string? Text { get; set; }

        internal bool ConsoleBlankLineBefore { get; set; }
        internal bool ConsoleBlankLineAfter { get; set; }

        internal string FormatForFile(bool useUtcTime)
        {
            return $"{(useUtcTime ? CreatedUtc : CreatedLocal):HH:mm:ss} {Severity.ToString().PadRight(7)} {Text}\r\n";
        }
    }
}
