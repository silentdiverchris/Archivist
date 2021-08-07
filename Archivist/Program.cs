using Archivist.Classes;
using Archivist.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Archivist
{
    class Program
    {
        static async Task Main(string[] args)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("AppSettings.json", true, true)
                .Build();

            // Read in and validate the config as much as possible

            string configFileName = config["ConfigurationFile"];
            string configFilePath = configFileName.Contains(Path.DirectorySeparatorChar)
                ? configFileName
                : Path.Join(AppDomain.CurrentDomain.BaseDirectory, configFileName);

            _ = bool.TryParse(config["DebugConsole"], out bool debugConsole);

            JobDetails jobDetails = new(
                configFilePath: configFilePath,
                jobName: args.Length > 0 ? args[0] : config["RunJobName"],
                sqlConnectionString: config.GetConnectionString("DefaultConnection"),
                logDirectoryName: config["LogDirectory"],
                aesEncryptExecutable: config["AESEncryptPath"],
                debugConsole: debugConsole);

            using (var archivist = new MainProcess(jobDetails))
            {
                await archivist.RunAsync();
            }
        }
    }
}
