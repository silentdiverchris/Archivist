using Archivist.Helpers;
using Archivist.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;
using static Archivist.Enumerations;

namespace Archivist
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                IConfiguration config = new ConfigurationBuilder()
                    .AddJsonFile("AppSettings.json", true, true)
                    .Build();

                // Read in and validate the config as much as possible

                string configFileName = config["ConfigurationFile"];

                // If not specified, use the default name in the install folder
                string configFilePath = configFileName.Contains(Path.DirectorySeparatorChar)
                    ? configFileName
                    : Path.Join(AppDomain.CurrentDomain.BaseDirectory, string.IsNullOrEmpty(configFileName) ? "configuration.json" : configFileName);

                _ = bool.TryParse(config["DebugConsole"], out bool debugConsole);
                _ = bool.TryParse(config["WriteProgressToEventLog"], out bool progressToEventlog);

                JobDetails jobDetails = new(
                    configFilePath: configFilePath,
                    jobName: args.Length > 0 ? args[0] : config["RunJobName"],
                    sqlConnectionString: config.GetConnectionString("DefaultConnection"),
                    logDirectoryName: config["LogDirectory"],
                    aesEncryptExecutable: config["AESEncryptPath"],
                    debugConsole: debugConsole,
                    progressToEventLog: progressToEventlog);

                EventLogHelper.WriteEntry($"Archivist starting job '{jobDetails.JobName}'", enSeverity.Info);

                using (var archivist = new MainProcess(jobDetails))
                {
                    await archivist.RunAsync();
                }
            }
            catch (Exception ex)
            {
                EventLogHelper.WriteEntry($"Exception in Main {ex.Message} {ex.Source}", enSeverity.Error);
                throw;
            }
        }
    }
}
