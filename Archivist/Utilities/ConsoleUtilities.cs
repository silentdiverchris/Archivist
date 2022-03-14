using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archivist.Utilities
{
    // Lifted and adapted from https://www.codeproject.com/Tips/5274597/An-Improved-Stream-CopyToAsync-That-Reports-Progre

    internal static class ConsoleUtilities
    {
        const char _block = '■';
        const char _space = ' ';
        const char _backSpace = '\b';

        const int _percentPerBlock = 5;
        const int _totalBlocks = 100 / _percentPerBlock;

        /// <summary>
        /// Display a natty progress indicator and %age complete, surrounded by custom text.
        /// This rather relies on getting the same prefix and suffix each time it is called 
        /// with a new percentage, or at least that they are the same length or longer than 
        /// the previous call or it won't completely obscure the previous text. If you are 
        /// going to make the output vary in length, add some extra spaces at the end with 
        /// the mildly hacky extraSpaces parameter to cover the difference.
        /// </summary>
        /// <param name="percentComplete">The percent complete value</param>
        /// <param name="prefix">Text to show before the progress display</param>
        /// <param name="suffix">Text to show after the progress display</param>
        /// <param name="extraSpaces">A number of spaces to write after the suffix, to clear longer text from last time</param>
        public static void WriteProgressBar(short percentComplete, string? prefix = null, string? suffix = null, short extraSpaces = 0)
        {
            if (percentComplete < 0)
            {
                percentComplete = 0;
            }
            else if (percentComplete > 100)
            {
                percentComplete = 100;
            }

            var howManyBlocks = (short)((percentComplete / _percentPerBlock) + .5f);

            // We don't display progress at 100%, it's assumed that the caller will write
            // a 'Completed successfully blah blah' message.

            // We need to build the output even if we're not going to display it because
            // at 100% we want to blank the entire line that we wrote last time, so need to know
            // how long the previous line was and this is static so we've no concept of last time

            string line =
                string.IsNullOrEmpty(prefix) ? "[" : prefix + " [" +
                new string(c: _block, count: howManyBlocks) +
                new string(c: _space, count: _totalBlocks - howManyBlocks) +
                $"] {percentComplete:N0}% " + 
                suffix +
                new string(c: _space, count: extraSpaces);

            // We append a backspace for each character we are about to write, so as to always leave the
            // cursor at the beginning of the line

            string backSpaces = new(c: _backSpace, count: line.Length);

            if (percentComplete == 100)
            { 
                // We're done, overwrite the line we wrote last time with spaces
                line = new string(c: _space, count: line.Length);
            }

            Console.Write(line + backSpaces);
        }
    }
}
