using Archivist.Classes;
using Archivist.Helpers;
using Archivist.Services;
using Archivist.Utilities;
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
            Result result = new("Program.Main");

            try
            {
                // Create default appsettings.json if it does not exist, this happens at first run, and if the
                // user deletes or moves it to generate a fresh one that they can customise.

                string appSettingsFileName = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

                if (!File.Exists(appSettingsFileName))
                {
                    AppSettingsUtilities.CreateDefaultAppSettings(appSettingsFileName);
                }

                AppSettings? appSettings = AppSettingsUtilities.LoadAppSettings(appSettingsFileName);

                if (appSettings is not null)
                {
                    string? jobName = args.Length > 0 ? args[0] : appSettings.DefaultJobName ?? null;

                    if (jobName is not null)
                    {
                        JobDetails jobDetails = new(jobName);

                        using (var archivist = new MainProcess(jobDetails, appSettings))
                        {
                            result.SubsumeResult(await archivist.Initialise());

                            if (!result.HasErrors)
                            {
                                result.SubsumeResult(await archivist.RunAsync());
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("Program.Main could not find a job name to run");
                    }
                }
                else
                {
                    throw new Exception("Program.Main could not generate an AppSettings object");
                }
            }
            catch (Exception ex)
            {
                EventLogHelpers.WriteEntry($"Exception in Archivist.Main {ex.Message}", enSeverity.Error);
                Console.WriteLine($"{ex.Message}");

                result.AddException(ex);
            }

#if DEBUG
            if (result.HasErrors)
            {
                Console.WriteLine("Press any key to exit");
                Console.ReadLine();
            }
#endif
        }
    }
}
