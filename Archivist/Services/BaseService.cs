using Archivist.Classes;
using System;

namespace Archivist.Services
{
    // No base functionality here, mainly just to enforce a standard interface for the constructor

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
