using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Archivist.Enumerations;

namespace Archivist.Classes
{
    internal class ResultMessage
    {
        internal ResultMessage(string text, enSeverity severity = enSeverity.Info, Exception ex = null, string functionName = null)
        {
            CreatedUtc = DateTime.UtcNow;
            Text = text;
            Severity = severity;
            Exception = ex;
            FunctionName = functionName;
        }

        internal DateTime CreatedUtc { get; private set; }
        internal enSeverity Severity { get; set; }
        internal string Text { get; set; }
        internal Exception Exception { get; set; }
        internal string FunctionName { get; set; }
        internal bool HasBeenWritten { get; set; }
    }

    internal class Result
    {
        internal DateTime CreatedUtc { get; private set; } = DateTime.UtcNow;

        internal List<ResultMessage> Messages { get; set; } = new();
        internal string FunctionName { get; private set; }

        internal List<ResultMessage> UnprocessedMessages => Messages.Where(_ => _.HasBeenWritten == false).ToList();

        internal int ItemsFound { get; set; }
        internal int ItemsProcessed { get; set; }
        internal long BytesProcessed { get; set; }

        internal enSeverity HighestSeverity => Messages.OrderBy(_ => _.Severity).First().Severity;

        internal bool HasErrors => Messages.Any(_ => _.Severity == enSeverity.Error);
        internal bool HasWarnings => Messages.Any(_ => _.Severity == enSeverity.Warning);

        internal Result(string functionName, bool addStartingItem = false, string appendText = null)
        {
            FunctionName = functionName;

            if (addStartingItem)
                AddInfo($"Starting {functionName} {appendText}");
        }

        internal void MarkMessagesWritten()
        {
            foreach (var msg in UnprocessedMessages)
            {
                msg.HasBeenWritten = true;
            }
        }

        internal void AddError(string text)
        {
            Messages.Add(new ResultMessage(text, severity: enSeverity.Error, functionName: FunctionName));
        }

        internal void AddWarning(string text)
        {
            Messages.Add(new ResultMessage(text, severity: enSeverity.Warning, functionName: FunctionName));
        }

        internal void AddInfo(string text)
        {
            Messages.Add(new ResultMessage(text, severity: enSeverity.Info, functionName: FunctionName));
        }

        internal void AddDebug(string text)
        {
            Messages.Add(new ResultMessage(text, severity: enSeverity.Debug, functionName: FunctionName));
        }

        internal void AddSuccess(string text)
        {
            Messages.Add(new ResultMessage(text, severity: enSeverity.Success, functionName: FunctionName));
        }

        internal void AddException(Exception ex)
        {
            Messages.Add(new ResultMessage(ex.Message, severity: enSeverity.Error, ex: ex, functionName: FunctionName));
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
        internal void SubsumeResult(Result result, bool AddItemCounts = true)
        {
            if (result == this)
            {
                throw new Exception("Cannot subsume a result into itself, that would be very silly");
            }

            foreach (var message in result.Messages)
            {
                Messages.Add(message);
            }

            if (AddItemCounts)
            {
                ItemsFound += result.ItemsFound;
                ItemsProcessed += result.ItemsProcessed;
                BytesProcessed += result.BytesProcessed;
            }
        }

        internal void ClearItemCounts()
        {
            ItemsFound += 0;
            ItemsProcessed += 0;
            BytesProcessed += 0;
        }

        internal string TextSummary
        {
            get
            {
                StringBuilder sb = new(200);

                foreach (var message in Messages)
                {
                    sb.AppendLine($"{message.Severity}: {message.Text}");
                }

                return sb.ToString();
            }
        }
    }
}
