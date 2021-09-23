using Archivist.Models;
using System;

namespace Archivist.Services
{
    internal class BaseService : IDisposable
    {
        protected readonly Job _jobSpec;
        protected readonly AppSettings _appSettings;
        protected readonly LogService _logService;

        internal BaseService(
            Job jobSpec,
            AppSettings appSettings,
            LogService logService)
        {
            _jobSpec = jobSpec;
            _appSettings = appSettings;
            _logService = logService;
        }

        public void Dispose()
        {
            // Nothing here to dispose
        }
    }
}
