﻿using Archivist.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Archivist.Enumerations;

namespace Archivist.Classes
{
    internal class ResultMessage
    {
        internal ResultMessage(
            string text, 
            enSeverity severity = enSeverity.Info, 
            Exception? ex = null, 
            string? functionName = null, 
            bool alwaysWriteToEventLog = false,
            bool consoleBlankLineBefore = false,
            bool consoleBlankLineAfter = false)
        {
            CreatedLocal = DateTime.Now;
            Text = text;
            Severity = severity;
            Exception = ex;
            FunctionName = functionName;
            AlwaysWriteToEventLog = alwaysWriteToEventLog;
            ConsoleBlankLineBefore = consoleBlankLineBefore;
            ConsoleBlankLineAfter = consoleBlankLineAfter;
        }

        internal DateTime CreatedLocal { get; private set; }
        internal enSeverity Severity { get; set; }
        internal string Text { get; set; }
        internal Exception? Exception { get; set; }
        internal string? FunctionName { get; set; }
        internal bool HasBeenProcessed { get; set; }
        internal bool AlwaysWriteToEventLog { get; set; }
        internal bool ConsoleBlankLineBefore { get; set; }
        internal bool ConsoleBlankLineAfter { get; set; }
    }

    internal class Result
    {
        internal DirectoryStatistics Statistics { get; set; } = new();

        internal DateTime CreatedTime { get; private set; } = DateTime.Now;

        internal List<ResultMessage> Messages { get; set; } = new();
        internal string FunctionName { get; private set; }

        internal List<ResultMessage> UnprocessedMessages => Messages.Where(_ => _.HasBeenProcessed == false).ToList();

        internal enSeverity HighestSeverity => Messages.OrderBy(_ => _.Severity).First()?.Severity ?? enSeverity.Debug;

        internal bool HasErrors => Messages.Any(_ => _.Severity == enSeverity.Error);
        internal bool HasWarnings => Messages.Any(_ => _.Severity == enSeverity.Warning);

        internal Result(string functionName, bool addStartingItem = false, string? functionQualifier = null, bool consoleBlankLineBefore = false)
        {
            FunctionName = functionName + functionQualifier.PrefixIfNotEmpty();

            if (addStartingItem)
            {
                AddInfo($"Running {functionName} {functionQualifier}", consoleBlankLineBefore: consoleBlankLineBefore);
            }
        }

        internal void MarkMessagesWritten()
        {
            foreach (var msg in UnprocessedMessages)
            {
                msg.HasBeenProcessed = true;
            }
        }

        internal void AddError(string text, bool consoleBlankLineBefore = false, bool consoleBlankLineAfter = false)
        {
            Messages.Add(new ResultMessage(text, severity: enSeverity.Error, functionName: FunctionName, consoleBlankLineBefore: consoleBlankLineBefore, consoleBlankLineAfter: consoleBlankLineAfter));
        }

        internal void AddWarning(string text, bool consoleBlankLineBefore = false, bool consoleBlankLineAfter = false)
        {
            Messages.Add(new ResultMessage(text, severity: enSeverity.Warning, functionName: FunctionName, consoleBlankLineBefore: consoleBlankLineBefore, consoleBlankLineAfter: consoleBlankLineAfter));
        }

        internal void AddInfo(string text, bool consoleBlankLineBefore = false, bool consoleBlankLineAfter = false)
        {
            Messages.Add(new ResultMessage(text, severity: enSeverity.Info, functionName: FunctionName, consoleBlankLineBefore: consoleBlankLineBefore, consoleBlankLineAfter: consoleBlankLineAfter));
        }

        internal void AddDebug(string text, bool consoleBlankLineBefore = false, bool consoleBlankLineAfter = false)
        {
            Messages.Add(new ResultMessage(text, severity: enSeverity.Debug, functionName: FunctionName, consoleBlankLineBefore: consoleBlankLineBefore, consoleBlankLineAfter: consoleBlankLineAfter));
        }

        internal void AddSuccess(string text, bool consoleBlankLineBefore = false, bool consoleBlankLineAfter = false)
        {
            Messages.Add(new ResultMessage(text, severity: enSeverity.Success, functionName: FunctionName, consoleBlankLineBefore: consoleBlankLineBefore, consoleBlankLineAfter: consoleBlankLineAfter));
        }

        internal void AddException(Exception ex, bool consoleBlankLineBefore = false, bool consoleBlankLineAfter = false)
        {
            Messages.Add(new ResultMessage($"{FunctionName} {ex.Message}", severity: enSeverity.Error, ex: ex, functionName: FunctionName));
        }

        internal bool HasNoErrors
        {
            get
            {
                return !HasErrors;
            }
        }

        internal bool HasNoErrorsOrWarnings
        {
            get
            {
                return !HasErrors & !HasWarnings;
            }
        }

        /// <summary>
        /// Copies the messages and optionally, item counts from the result into the caller
        /// </summary>
        /// <param name="result"></param>
        /// <param name="AddItemCounts">Whether to add the ItemsFound, ItemsProcessed and BytesProcessed to the ones in the caller</param>
        internal void SubsumeResult(Result result, bool addStatistics = true)
        {
            if (result == this)
            {
                throw new Exception("Cannot subsume a result into itself, that would be very silly");
            }

            foreach (var message in result.Messages)
            {
                Messages.Add(message);
            }

            if (addStatistics)
            {
                Statistics.SubsumeStatistics(result.Statistics);
            }
        }

        internal void ClearStatistics()
        {
            Statistics = new DirectoryStatistics();
        }

        internal string TextSummary
        {
            get
            {
                StringBuilder sb = new(300);

                foreach (var message in Messages)
                {
                    sb.AppendLine($"{message.Severity}: {message.Text}");
                }

                return sb.ToString();
            }
        }
    }
}
