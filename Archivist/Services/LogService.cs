using Archivist.Classes;
using Archivist.Helpers;
using Archivist.Models;
using Archivist.Utilities;
using Microsoft.Data.SqlClient;
using System.Security.AccessControl;
using System.Text;
using static Archivist.Delegates;
using static Archivist.Enumerations;

namespace Archivist.Services
{
    internal class LogService : IDisposable
    {
        private readonly bool _logToSql = false;
        private string _logToSqlDatabaseName = null;

        private readonly bool _logToFile = false;
        private readonly bool _logToConsole = true; // No way to turn this off at present

        public bool LoggingToSql => _logToSql;
        public string LoggingToSqlDatabaseName => _logToSqlDatabaseName;

        public bool LoggingToFile => _logToFile;
        public bool LoggingToConsole => _logToConsole;

        private readonly JobDetails _jobDetails = null;
        private readonly string _logFileName = null;

        private readonly ConsoleDelegate _consoleDelegate;

        internal string LogFileName => _logFileName;

        /// <summary>
        /// If we get a connection string, log to SQL
        /// If we get a log directory name, log to file
        /// Or both or neither.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="logDirectoryName"></param>
        internal LogService(JobDetails jobDetails, ConsoleDelegate consoleDelegate)
        {
            _consoleDelegate = consoleDelegate;
            _jobDetails = jobDetails;

            if (!string.IsNullOrEmpty(_jobDetails.SqlConnectionString))
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

            if (!string.IsNullOrEmpty(_jobDetails.LogDirectoryPath))
            {
                if (!_jobDetails.LogDirectoryPath.Contains(Path.DirectorySeparatorChar))
                {
                    // Just the directory name, we will asume it's under the install directory
                    _jobDetails.AppSettings.LogDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _jobDetails.LogDirectoryPath);
                }

                if (!Directory.Exists(_jobDetails.LogDirectoryPath))
                {
                    try
                    {
                        Directory.CreateDirectory(_jobDetails.LogDirectoryPath);

                        // Allow autheticated users to write to the log directory

                        var Info = new DirectoryInfo(_jobDetails.LogDirectoryPath);

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
                        string msg = $"Log directory '{_jobDetails.LogDirectoryPath}' does not exist and cannot be created, remove the setting or ensure it refers to an existing directory or one that can be created.";
                        EventLogHelpers.WriteEntry(msg, enSeverity.Error);
                        throw new Exception(msg, ex);
                    }
                }

                if (Directory.Exists(_jobDetails.LogDirectoryPath))
                {
                    _logFileName = Path.Combine(_jobDetails.LogDirectoryPath, $"Archivist-{_jobDetails.JobNameToRun}-{DateTime.UtcNow.ToString(Constants.DATE_FORMAT_DATE_TIME_YYYYMMDDHHMMSS)}.log");
                    _logToFile = true;
                }
            }
        }

        /// <summary>
        /// Send each unprocessed message in the result to the configured log destinations
        /// </summary>
        /// <param name="result">The result</param>
        /// <param name="addCompletionItem">To be called at the end of a function, or functional unit, it logs error and warning counts or success</param>
        /// <param name="reportItemCounts">Logs the figures in ItemsFound, ItemsProcessed and BytesProcessed</param>
        /// <returns></returns>
        internal async Task ProcessResult(
            Result result, 
            bool addCompletionItem = false, 
            bool reportItemCounts = false,
            string itemNameSingular = null)
        {
            foreach (var msg in result.UnprocessedMessages.OrderBy(_ => _.CreatedUtc))
            {
                await AddLogAsync(
                    new LogEntry(
                        logText: msg.Text,
                        severity: msg.Severity,
                        createdUtc: msg.CreatedUtc));

                if (msg.Exception is not null)
                {
                    await AddLogAsync(
                        new LogEntry (
                            logText: msg.Exception.Message,
                            severity: msg.Severity,
                            createdUtc: msg.CreatedUtc));

                    if (msg.Exception.InnerException is not null)
                    {
                        await AddLogAsync(
                            new LogEntry(
                                logText: msg.Exception.InnerException.Message,
                                severity: msg.Severity,
                                createdUtc: msg.CreatedUtc));
                    }
                }
            }

            if (reportItemCounts)
            {
                if (result.ItemsFound > 0)
                {
                    if (result.ItemsProcessed > 0)
                    {
                        result.AddInfo($"{result.ItemsFound} {itemNameSingular.Pluralise(result.ItemsFound, " ")}found, {result.ItemsProcessed} processed ({FileUtilities.GetByteSizeAsText(result.BytesProcessed)})");                
                    }
                    else
                    {
                        result.AddInfo($"{result.ItemsFound} {itemNameSingular.Pluralise(result.ItemsFound, " ")}found, none needed processing");
                    }
                }
                else
                {
                    result.AddInfo($"No {itemNameSingular.Pluralise(result.ItemsFound)} found to process");
                }
            }

            if (addCompletionItem)
            {
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

            result.MarkMessagesWritten();
        }

        private async Task AddLogAsync (LogEntry entry)
        {
            if (_jobDetails.AppSettings.VerboseEventLog || entry.AlwaysWriteToEventLog || entry.Severity == enSeverity.Warning || entry.Severity == enSeverity.Error)
            {
                EventLogHelpers.WriteEntry(entry.Text, severity: entry.Severity);
            }

            if (_logToConsole && (_jobDetails.AppSettings.VerboseConsole || entry.Severity != enSeverity.Debug))
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

        private Result VerifyAndPrepareDatabase()
        {
            // test database can be got at and initialise it if the log table doesn't exist

            Result result = new("VerifyAndPrepareDatabase", false);

            try
            {
                using (SqlConnection conn = new(_jobDetails.SqlConnectionString))
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

            await SQLUtilities.ExecuteStoredProcedureNonQueryAsync(_jobDetails.SqlConnectionString, "AddLogEntry", parameters);
        }

        private async Task AddLogToFileAsync(LogEntry entry)
        {
            if (_logToFile)
            {
                using (FileStream sourceStream = new(_logFileName, FileMode.Append, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
                {
                    byte[] encodedText = Encoding.Unicode.GetBytes(entry.FormatForFile());
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
