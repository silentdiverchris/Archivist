//using Archivist.Classes;
//using Archivist.Models;
//using Microsoft.Data.SqlClient;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using static Archivist.Enumerations;
//using static Archivist.Models.LogEntry;

//namespace Archivist.Helpers
//{
//    internal static class LogService
//    {
//        //internal static async Task AddLogToSqlAsync(Result result, LogEntry templateLogEntry = null)
//        //{
//        //    enSeverity severity = result.HasErrors ? enSeverity.Error : (result.HasWarnings ? enSeverity.Warning : enSeverity.Info);

//        //    await AddLogToSqlAsync(result.TextSummary, severity, templateLogEntry);
//        //}

//        internal static async Task AddLogToSqlAsync(string logText, enSeverity severity = enSeverity.Info, LogEntry templateLogEntry = null)
//        {
//            if (templateLogEntry is not null)
//            {
//                await AddLogToSqlAsync(new LogEntry()
//                {
//                    Text = logText,
//                    Severity = severity,
//                    BackupTypeName = templateLogEntry.BackupTypeName,
//                    FunctionName = templateLogEntry.FunctionName,
//                    SourceFileName = templateLogEntry.SourceFileName,
//                    SourceDirectoryName = templateLogEntry.SourceDirectoryName,
//                    DestinationFileName = templateLogEntry.DestinationFileName,
//                    DestinationDirectoryName = templateLogEntry.DestinationDirectoryName
//                }); 
//            }
//            else
//            {
//                await AddLogToSqlAsync(new LogEntry(logText: logText, severity: severity));
//            }
//        }

//        internal static async Task AddLogToSqlAsync(LogEntry entry)
//        {
//            List<SqlParameter> parameters = StoredProcedureHelpers.BuildSQLParameterList(
//                "FileSizeBytes", entry.FileSizeBytes,
//                   "BackupTypeName", entry.BackupTypeName,
//                   "FunctionName", entry.FunctionName,
//                   "SourceFileName", entry.SourceFileName,
//                   "DestinationFileName", entry.DestinationFileName,
//                   "SourceDirectoryName", entry.SourceDirectoryName,
//                   "DestinationDirectoryName", entry.DestinationDirectoryName,
//                   "LogText", entry.Text,
//                   "LogSeverity", (int)entry.Severity);

//            await StoredProcedureHelpers.ExecuteStoredProcedureNonQueryAsync(Constants.CONNECTION_STRING, "AddLogEntry", parameters);
//        }

//        //internal static void AddLogToSql(LogEntry entry)
//        //{
//        //    List<SqlParameter> parameters = StoredProcedureHelpers.BuildSQLParameterList(
//        //           "BackupTypeName", entry.BackupTypeName,
//        //           "FunctionName", entry.FunctionName,
//        //           "SourceFileName", entry.SourceFileName,
//        //           "DestinationFileName", entry.DestinationFileName,
//        //           "SourceDirectoryName", entry.SourceDirectoryName,
//        //           "DestinationDirectoryName", entry.DestinationDirectoryName,
//        //           "LogText", entry.Text,
//        //           "LogSeverity", (int)entry.Severity);

//        //    StoredProcedureHelpers.ExecuteStoredProcedureNonQuery(Constants.CONNECTION_STRING, "AddLogEntry", parameters);
//        //}
//    }
//}
