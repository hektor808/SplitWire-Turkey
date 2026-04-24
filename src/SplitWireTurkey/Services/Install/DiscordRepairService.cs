using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using SplitWireTurkey.Services.InstallContracts;

namespace SplitWireTurkey.Services.Install
{
    public class DiscordRepairService
    {
        private readonly SystemConfigService _systemConfigService;

        public DiscordRepairService(SystemConfigService systemConfigService)
        {
            _systemConfigService = systemConfigService;
        }

        public OperationResult CloseProcess(string processName)
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    process.Kill();
                }

                return OperationResult.Ok($"{processName} kapatıldı");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }

        public OperationResult Uninstall(string updaterPath, string uninstallArgs)
        {
            return _systemConfigService.Execute(new CommandRequest(updaterPath, uninstallArgs));
        }

        public async Task<OperationResult> InstallAsync(DiscordInstallRequest request, string downloadDirectory)
        {
            try
            {
                Directory.CreateDirectory(downloadDirectory);
                var installerPath = Path.Combine(downloadDirectory, request.InstallerName);

                using var client = new HttpClient();
                await using (var output = File.Create(installerPath))
                await using (var stream = await client.GetStreamAsync(request.InstallerUrl))
                {
                    await stream.CopyToAsync(output);
                }

                return _systemConfigService.Execute(new CommandRequest(installerPath, string.Empty));
            }
            catch (Exception ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }
    }
}
