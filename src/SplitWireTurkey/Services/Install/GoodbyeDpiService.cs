using SplitWireTurkey.Services.InstallContracts;

namespace SplitWireTurkey.Services.Install
{
    public class GoodbyeDpiService
    {
        private readonly SystemConfigService _systemConfigService;

        public GoodbyeDpiService(SystemConfigService systemConfigService)
        {
            _systemConfigService = systemConfigService;
        }

        public OperationResult Install(ServiceInstallRequest request)
        {
            var result = _systemConfigService.Execute(new CommandRequest("sc", $"create {request.ServiceName} binPath= \"{request.Parameters}\" start= auto"));
            if (!string.IsNullOrWhiteSpace(request.LogPath))
            {
                _systemConfigService.AppendLog(request.LogPath, $"GoodbyeDPI install sonucu: {result.Message}");
            }

            return result;
        }

        public OperationResult Start(string serviceName, string? logPath = null)
        {
            var result = _systemConfigService.Execute(new CommandRequest("net", $"start {serviceName}"));
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                _systemConfigService.AppendLog(logPath, $"GoodbyeDPI start sonucu: {result.Message}");
            }

            return result;
        }

        public OperationResult Remove(string serviceName, string? logPath = null)
        {
            var result = _systemConfigService.Execute(new CommandRequest("sc", $"delete {serviceName}"));
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                _systemConfigService.AppendLog(logPath, $"GoodbyeDPI remove sonucu: {result.Message}");
            }

            return result;
        }
    }
}
