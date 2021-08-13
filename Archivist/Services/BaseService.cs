using Archivist.Models;

namespace Archivist.Services
{
    internal class BaseService : IDisposable
    {
        protected readonly JobSpecification _jobSpec;
        protected readonly LogService _logService;

        internal BaseService(
            JobSpecification jobSpec,
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
