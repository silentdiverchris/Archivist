using Archivist.Classes;
using Archivist.Helpers;
using Archivist.Models;
using Archivist.Utilities;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using static Archivist.Delegates;
using static Archivist.Enumerations;

namespace Archivist.Services
{
    internal class LogService : IDisposable
    {
        private readonly bool _logToSql = false;
        private string? _logToSqlDatabaseName = null;

        private readonly bool _logToFile = false;
        private readonly bool _logToConsole = true; // No way to turn this off at present

        public bool LoggingToSql => _logToSql;
        public string? LoggingToSqlDatabaseName => _logToSqlDatabaseName;

        public bool LoggingToFile => _logToFile;
        public bool LoggingToConsole => _logToConsole;

        private readonly JobDetails? _jobDetails = null;
        private readonly AppSettings _appSettings;
        private readonly string? _logFileName = null;

        private readonly ConsoleDelegate _consoleDelegate;

        internal string? LogFileName => _logFileName;

        /// <summary>
        /// If we get a connection string, log to SQL
        /// If we get a log directory name, log to file
        /// Or both or neither.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="logDirectoryName"></param>
        internal LogService(
            JobDetails jobDetails, 
            AppSettings appSettings, 
            ConsoleDelegate consoleDelegate)
        {
            _consoleDelegate = consoleDelegate;
            _jobDetails = jobDetails;
            _appSettings = appSettings;

            if (!string.IsNullOrEmpty(_appSettings.SqlConnectionString))
            {
                Result result = VerifyAndPrepareDatabase();

                if (result.HasNoErrorsOrWarnings)
                {
                    _logToSql = true;
                }
                else 
                {
                    result.AddError("Logging to SQL not enabled, database doesn't exist or cannot be initialised");
                }
            }

            if (!string.IsNullOrEmpty(_appSettings.LogDirectoryPath))
            {
                if (!_appSettings.LogDirectoryPath.Contains(Path.DirectorySeparatorChar))
                {
                    // Just the directory name, we will asume it's under the install directory
                    _appSettings.LogDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _appSettings.LogDirectoryPath);
                }

                if (!Directory.Exists(_appSettings.LogDirectoryPath))
                {
                    try
                    {
                        Directory.CreateDirectory(_appSettings.LogDirectoryPath);

                        // Allow autheticated users to write to the log directory

                        var Info = new DirectoryInfo(_appSettings.LogDirectoryPath);

#pragma warning disable CA1416 // Validate platform compatibility

                        var Security = Info.GetAccessControl(AccessControlSections.Access);

                        Security.AddAccessRule(
                            rule: new FileSystemAccessRule(
                                identity: "Authenticated Users",
                                fileSystemRights: FileSystemRights.Modify,
                                inheritanceFlags: InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                                propagationFlags: PropagationFlags.InheritOnly,
                                type: AccessControlType.Allow));

#pragma warning restore CA1416 // Validate platform compatibility

                    }
                    catch (Exception ex)
                    {
                        string msg = $"Log directory '{_appSettings.LogDirectoryPath}' does not exist and cannot be created, remove the setting or ensure it refers to an existing directory or one that can be created.";
                        EventLogHelpers.WriteEntry(msg, enSeverity.Error);
                        throw new Exception(msg, ex);
                    }
                }

                if (Directory.Exists(_appSettings.LogDirectoryPath))
                {
                    DateTime useDate = _appSettings.UseUtcTime ? DateTime.UtcNow : DateTime.Now;
                    string fileName = $"Archivist-{_jobDetails.JobNameToRun}-{useDate.ToString(Constants.DATE_FORMAT_DATE_TIME_YYYYMMDDHHMMSS)}.log";
                    _logFileName = Path.Combine(_appSettings.LogDirectoryPath, fileName);
                    _logToFile = true;
                }
            }
        }

        /// <summary>
        /// Send each unprocessed message in the result to the configured log destinations and optionally
        /// log statistics and completion messages.
        /// </summary>
        /// <param name="result">The result</param>
        /// <param name="reportCompletion">To be called at the end of a function, or functional unit, it logs error and warning counts or success</param>
        /// <param name="reportItemCounts">Logs the statistics in ItemsFound, ItemsProcessed and BytesProcessed</param>
        /// <param name="reportAllStatistics">Logs detailed file and byte statistics</param>
        /// <returns></returns>
        internal async Task ProcessResult(
            Result result, 
            bool reportCompletion = false, 
            bool reportItemCounts = false,
            bool reportAllStatistics = false)
        {
            bool addBlankLine = false;

            if (reportItemCounts)
            {
                if (result.Statistics.ItemsFound > 0)
                {
                    addBlankLine = true;

                    if (result.Statistics.ItemsProcessed > 0)
                    {
                        result.AddInfo($"{result.Statistics.ItemsFound} {"file".Pluralise(result.Statistics.ItemsFound, " ")}found, {result.Statistics.ItemsProcessed} processed ({FileUtilities.GetByteSizeAsText(result.Statistics.BytesProcessed)}) by {result.FunctionName}");
                    }
                    else
                    {
                        result.AddDebug($"{result.Statistics.ItemsFound} {"file".Pluralise(result.Statistics.ItemsFound, " ")}found, none needed processing by {result.FunctionName}");
                    }
                }
                else
                {
                    result.AddDebug($"No {"file".Pluralise(result.Statistics.ItemsFound)} found to process by {result.FunctionName}");
                }
            }

            if (reportAllStatistics)
            {
                if (result.Statistics.FilesAdded > 0)
                {
                    addBlankLine = true;
                    result.AddInfo($"{FileUtilities.GetByteSizeAsText(result.Statistics.BytesAdded)} added to {result.Statistics.FilesAdded:N0} file{result.Statistics.FilesAdded.PluralSuffix()} by {result.FunctionName}");
                }

                if (result.Statistics.FilesDeleted > 0)
                {
                    addBlankLine = true;
                    result.AddInfo($"{FileUtilities.GetByteSizeAsText(result.Statistics.BytesDeleted)} deleted from {result.Statistics.FilesDeleted:N0} file{result.Statistics.FilesDeleted.PluralSuffix()} by {result.FunctionName}");
                }
            }

            foreach (var msg in result.UnprocessedMessages.OrderBy(_ => _.CreatedUtc))
            {
                await AddLogAsync(
                    new LogEntry(
                        logText: msg.Text,
                        severity: msg.Severity,
                        consoleBlankLineBefore: msg.ConsoleBlankLineBefore,
                        consoleBlankLineAfter: msg.ConsoleBlankLineAfter));

                if (msg.Exception is not null)
                {
                    await AddLogAsync(
                        new LogEntry (
                            logText: msg.Exception.Message,
                            severity: msg.Severity,
                            consoleBlankLineBefore: msg.ConsoleBlankLineBefore,
                            consoleBlankLineAfter: msg.ConsoleBlankLineAfter));

                    if (msg.Exception.InnerException is not null)
                    {
                        await AddLogAsync(
                            new LogEntry(
                                logText: msg.Exception.InnerException.Message,
                                severity: msg.Severity,
                                consoleBlankLineBefore: msg.ConsoleBlankLineBefore,
                                consoleBlankLineAfter: msg.ConsoleBlankLineAfter));
                    }
                }
            }

            if (reportCompletion)
            {
                addBlankLine = false;

                if (result.HasErrors)
                {
                    result.AddError($"{result.FunctionName} completed with errors");
                }
                else if (result.HasWarnings)
                {
                    result.AddWarning($"{result.FunctionName} completed with warnings");
                }
                else
                {
                    result.AddSuccess($"{result.FunctionName} completed OK");
                }
            }

            if (addBlankLine)
            {
                _consoleDelegate.Invoke(new LogEntry(""));
            }

            result.MarkMessagesWritten();
        }

        /// <summary>
        /// The primary 'log something' interface
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        private async Task AddLogAsync (LogEntry entry)
        {
            if (entry.Text is not null && _appSettings.VerboseEventLog || entry.AlwaysWriteToEventLog || entry.Severity == enSeverity.Warning || entry.Severity == enSeverity.Error)
            {
                EventLogHelpers.WriteEntry(entry.Text!, severity: entry.Severity);
            }

            if (_logToConsole && (_appSettings.VerboseConsole || entry.Severity != enSeverity.Debug))
            {
                _consoleDelegate.Invoke(entry);
            }

            if (_logToSql)
            {
                await AddLogToSqlAsync(entry);
            }

            if (_logToFile)
            {
                await AddLogToFileAsync(entry);
            }
        }

        /// <summary>
        /// Used to check and if necessary, initialise the SQL entities to enable logging to SQL
        /// </summary>
        /// <returns></returns>
        private Result VerifyAndPrepareDatabase()
        {
            // Test database is accessible and initialise it if the AddLogEntry stored procedure doesn't exist

            Result result = new("VerifyAndPrepareDatabase", false);

            try
            {
                using (SqlConnection conn = new(_appSettings.SqlConnectionString))
                {
                    conn.Open();

                    _logToSqlDatabaseName = conn.Database;

                    string sql = "Select Case When Exists (Select * From sys.objects Where type = 'P' And OBJECT_ID = OBJECT_ID('dbo.AddLogEntry')) Then 1 Else 0 End";

                    SqlCommand cmd = new(sql, conn);

                    var storedProcExists = cmd.ExecuteScalar().ToString(); 

                    if (storedProcExists == "0")
                    {
                        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "InitialiseDatabase.sql");
                        sql = System.IO.File.ReadAllText(path);

                        // Use Sql Management Objects as the script is multi-statement
                        Microsoft.SqlServer.Management.Smo.Server server = new(new Microsoft.SqlServer.Management.Common.ServerConnection(conn));
                        server.ConnectionContext.ExecuteNonQuery(sql);
                    }
                }
            }
            catch (Exception ex)
            {
                result.AddException(ex);
            }

            return result;
        }

        private async Task AddLogToSqlAsync(LogEntry entry)
        {
            List<SqlParameter> parameters = SQLUtilities.BuildSQLParameterList(
                   "LogText", entry.Text,
                   "LogSeverity", (int)entry.Severity);

            await SQLUtilities.ExecuteStoredProcedureNonQueryAsync(_appSettings.SqlConnectionString!, "AddLogEntry", parameters);
        }

        private async Task AddLogToFileAsync(LogEntry entry)
        {
            if (_logToFile)
            {
                using (FileStream sourceStream = new(_logFileName!, FileMode.Append, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
                {
                    byte[] encodedText = Encoding.Unicode.GetBytes(entry.FormatForFile(_appSettings.UseUtcTime));
                    await sourceStream.WriteAsync(encodedText);
                };
            }
        }

        public void Dispose()
        {
            // We open a connection on demand each time so nothing to do here, though that might change, either way it's nice to be asked.
        }

        internal void LogToConsole(LogEntry entry)
        {
            if (_logToConsole)
            {
                _consoleDelegate.Invoke(entry);
            }
        }
    }
}
