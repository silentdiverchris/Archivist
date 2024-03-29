﻿using System;
using System.Diagnostics;
using static Archivist.Enumerations;

namespace Archivist.Helpers
{
    /// <summary>
    /// This uses System.Diagnostics so works fine on Windows but isn't truly platform-independent
    /// </summary>
    internal static class EventLogHelpers
    {
        internal static void WriteEntry(string text, enSeverity severity)
        {
            try
            {
    #pragma warning disable CA1416 // Validate platform compatibility
                var type = severity switch
                {
                    enSeverity.Error => EventLogEntryType.Error,
                    enSeverity.Warning => EventLogEntryType.Warning,
                    _ => EventLogEntryType.Information
                };

                EventLog.WriteEntry("Archivist", message: text, type: type);
    #pragma warning restore CA1416 // Validate platform compatibility
            }
            catch (Exception ex)
            {
                // Failed to write to event log, don't throw 
                Console.WriteLine($"Cannot write to event log, exception {ex.Message}");
            }
        }
    }
}
