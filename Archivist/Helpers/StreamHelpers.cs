using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Archivist.Helpers
{
    // Fairly shamelessly copied and slightly adapted from https://www.codeproject.com/Tips/5274597/An-Improved-Stream-CopyToAsync-That-Reports-Progre as per MIT licence.
    // Thanks to the curously named author 'Honey the code witch' https://www.codeproject.com/script/Membership/View.aspx?mid=11540398

    internal static class StreamHelpers
    {
        /// <summary>
        /// Copy a stream to another stream whilst oozing progress information
        /// </summary>
        /// <param name="source">The source <see cref="Stream"/> to copy from</param>
        /// <param name="sourceLength">The length of the source stream if known - used for progress reporting</param>
        /// <param name="destination">The destination <see cref="Stream"/> to copy to</param>
        /// <param name="bufferSize">The size of the copy block buffer</param>
        /// <param name="progress">An <see cref="IProgress{T}"/> implementation for reporting progress</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>A task representing the operation</returns>
        public static async Task CopyToAsyncProgress(
            this Stream source,
            long sourceLength,
            Stream destination,
            IProgress<KeyValuePair<long, long>> progress,
            CancellationToken cancellationToken,
            int bufferSize = Constants.STREAM_BUFFER_SIZE)
        {
            var buffer = new byte[bufferSize];

            if (sourceLength > 0 && source.CanSeek)
                sourceLength = source.Length - source.Position;

            var totalBytesCopied = 0L;

            if (progress is not null)
                progress.Report(new KeyValuePair<long, long>(totalBytesCopied, sourceLength));

            var bytesRead = -1;

            while (bytesRead != 0 && !cancellationToken.IsCancellationRequested)
            {
                bytesRead = await source.ReadAsync(buffer);

                if (bytesRead == 0 || cancellationToken.IsCancellationRequested)
                    break;

                await destination.WriteAsync(buffer);

                totalBytesCopied += bytesRead;

                if (progress is not null)
                {
                    progress.Report(new KeyValuePair<long, long>(totalBytesCopied, sourceLength));
                }
            }

            if (totalBytesCopied > 0 && progress is not null)
            {
                progress.Report(new KeyValuePair<long, long>(totalBytesCopied, sourceLength));
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
