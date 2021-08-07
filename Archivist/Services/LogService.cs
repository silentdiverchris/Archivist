using Archivist.Classes;
using Archivist.Helpers;
using Archivist.Models;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Archivist.Delegates;
using static Archivist.Enumerations;

namespace Archivist.Services
{
    internal class LogService : IDisposable
    {
        private readonly bool _logToSql = false;
        private readonly bool _logToFile = false;
        private readonly bool _logToConsole = true; // No way to turn this off at present

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

            if (!string.IsNullOrEmpty(_jobDetails.LogDirectoryName))
            {
                if (Directory.Exists(_jobDetails.LogDirectoryName))
                {
                    _logFileName = Path.Combine(_jobDetails.LogDirectoryName, $"Archivist-{_jobDetails.JobName}-{DateTime.UtcNow.ToString(Constants.DATE_FORMAT_DATE_TIME_YYYYMMDDHHMMSS)}.log");
                    _logToFile = true;

                    //_consoleDelegate.Invoke(new LogEntry($"Logging to {_logFileName}"));
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
            if (reportItemCounts)
            {
                if (result.ItemsFound > 0)
                {
                    if (result.ItemsProcessed > 0)
                    {
                        result.AddInfo($"{result.ItemsFound} {itemNameSingular.Pluralise(result.ItemsFound, " ")}found, {result.ItemsProcessed} processed ({FileHelpers.GetByteSizeAsText(result.BytesProcessed)})");                
                    }
                    else
                    {
                        result.AddInfo($"{result.ItemsFound} {itemNameSingular.Pluralise(result.ItemsFound, " ")}found, none processed");
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

            foreach (var msg in result.UnprocessedMessages.OrderBy(_ => _.CreatedUtc))
            {
                await AddLogAsync(
                    new LogEntry(
                        logText: msg.Text,
                        severity: msg.Severity,
                        createdUtc: msg.CreatedUtc));
            }

            result.MarkMessagesWritten();
        }

        private async Task AddLogAsync (LogEntry entry)
        {
            if (_logToConsole && (_jobDetails.DebugConsole || entry.Severity != enSeverity.Debug))
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

        private async Task AddLogAsync(string logText, enSeverity severity = enSeverity.Info)
        {
            LogEntry entry = new()
            {
                Text = logText,
                Severity = severity
            };

            await AddLogAsync(entry);
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

                    string sql = "SELECT CASE WHEN OBJECT_ID('dbo.Log', 'U') IS NOT NULL THEN 1 ELSE 0 END";

                    SqlCommand cmd = new(sql, conn);

                    var logTableExists = cmd.ExecuteScalar().ToString(); 

                    if (logTableExists == "0")
                    {
                        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "InitialiseDatabase.sql");
                        sql = File.ReadAllText(path);

                        // Use Sql Management Objects as the script is multi-statement
                        Server server = new Server(new ServerConnection(conn));
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
            List<SqlParameter> parameters = StoredProcedureHelpers.BuildSQLParameterList(
                   "LogText", entry.Text,
                   "LogSeverity", (int)entry.Severity);

            await StoredProcedureHelpers.ExecuteStoredProcedureNonQueryAsync(_jobDetails.SqlConnectionString, "AddLogEntry", parameters);
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
    }
}
