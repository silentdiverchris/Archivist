using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archivist.Utilities
{
    // Inspired by https://www.codeproject.com/Tips/5274597/An-Improved-Stream-CopyToAsync-That-Reports-Progre

    internal static class ConsoleUtilities
    {
        const char _block = '■';
        const int _percentPerBlock = 5;
        const int _totalBlocks = 100 / _percentPerBlock;
        const char _backSpace = '\b';

        public static void WriteProgressBar(short percent, string prefix, string suffix)
        {
            if (percent < 0)
            {
                percent = 0;
            }
            else if (percent > 100)
            {
                percent = 100;
            }

            var howManyBlocks = (short)((percent / _percentPerBlock) + .5f);

            // We need to calculate the length even if we aren't going to display
            // it because at 100% we want to blank the whole line. We never 
            // show 100%, so just allow 2 characters for % complete

            string line =
                prefix + " [" +
                new string(_block, howManyBlocks) +
                new string(' ', _totalBlocks - howManyBlocks) +
                $"] {percent:N0}% " + suffix;

            if (percent < 100)
            {
                line += new string(_backSpace, line.Length);

                Console.Write(line);
            }
            else
            {
                // We're done, clear the progress bar and text, the blank line will be used by 
                // whatever writes to the console next
                Console.Write(new string(' ', line.Length) + new string('\b', line.Length));
            }
        }
    }
}
