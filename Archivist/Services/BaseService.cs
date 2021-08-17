using Archivist.Models;

namespace Archivist.Services
{
    internal class BaseService : IDisposable
    {
        protected readonly Job _jobSpec;
        protected readonly LogService _logService;

        internal BaseService(
            Job jobSpec,
            LogService logService)
        {
            _jobSpec = jobSpec;
            _logService = logService;
        }

        public void Dispose()
        {
            // Nothing here to dispose
        }
    }
}
