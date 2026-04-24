using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SplitWireTurkey.Services
{
    public class ServiceManager
    {
        public async Task<(string serviceName, bool isInstalled)> CheckServiceAsync(string serviceName)
        {
            try
            {
                return await Task.Run(() =>
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "sc",
                        Arguments = $"query {serviceName}",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(startInfo);
                    var output = process?.StandardOutput.ReadToEnd() ?? string.Empty;
                    process?.WaitForExit();
                    var isInstalled = output.Contains("RUNNING") || output.Contains("STOPPED") || output.Contains("SERVICE_NAME");
                    return (serviceName, isInstalled);
                });
            }
            catch
            {
                return (serviceName, false);
            }
        }

        public async Task<Dictionary<string, bool>> CheckServicesAsync(IEnumerable<string> serviceNames)
        {
            var checks = new List<Task<(string serviceName, bool isInstalled)>>();
            foreach (var serviceName in serviceNames)
            {
                checks.Add(CheckServiceAsync(serviceName));
            }

            var results = await Task.WhenAll(checks);
            var map = new Dictionary<string, bool>();
            foreach (var (serviceName, isInstalled) in results)
            {
                map[serviceName] = isInstalled;
            }

            return map;
        }
    }
}
