using SplitWireTurkey.Services.InstallContracts;

namespace SplitWireTurkey.Services.Install
{
    public class ZapretService
    {
        private readonly SystemConfigService _systemConfigService;

        public ZapretService(SystemConfigService systemConfigService)
        {
            _systemConfigService = systemConfigService;
        }

        public OperationResult Install(ServiceInstallRequest request)
        {
            var result = _systemConfigService.Execute(new CommandRequest("sc", $"create {request.ServiceName} binPath= \"{request.Parameters}\" start= auto"));
            if (!string.IsNullOrWhiteSpace(request.LogPath))
            {
                _systemConfigService.AppendLog(request.LogPath, $"Zapret install sonucu: {result.Message}");
            }

            return result;
        }

        public OperationResult Remove(string serviceName, string? logPath = null)
        {
            var result = _systemConfigService.Execute(new CommandRequest("sc", $"delete {serviceName}"));
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                _systemConfigService.AppendLog(logPath, $"Zapret remove sonucu: {result.Message}");
            }

            return result;
        }
    }
}
