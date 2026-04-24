using System.Threading.Tasks;
using SplitWireTurkey.Services.InstallContracts;

namespace SplitWireTurkey.Services.Install
{
    public class InstallOrchestrator
    {
        private readonly SystemConfigService _systemConfigService;
        private readonly ZapretService _zapretService;
        private readonly GoodbyeDpiService _goodbyeDpiService;
        private readonly DiscordRepairService _discordRepairService;

        public InstallOrchestrator(
            SystemConfigService systemConfigService,
            ZapretService zapretService,
            GoodbyeDpiService goodbyeDpiService,
            DiscordRepairService discordRepairService)
        {
            _systemConfigService = systemConfigService;
            _zapretService = zapretService;
            _goodbyeDpiService = goodbyeDpiService;
            _discordRepairService = discordRepairService;
        }

        public OperationResult InstallZapret(ServiceInstallRequest request) => _zapretService.Install(request);

        public OperationResult InstallGoodbyeDpi(ServiceInstallRequest request) => _goodbyeDpiService.Install(request);

        public async Task<OperationResult> RepairDiscordAsync(DiscordInstallRequest request, string downloadDirectory)
        {
            var closeStable = _discordRepairService.CloseProcess("Discord");
            var closePtb = _discordRepairService.CloseProcess("DiscordPTB");

            if (!closeStable.Success && !closePtb.Success)
            {
                return OperationResult.Fail("Discord süreçleri kapatılamadı");
            }

            return await _discordRepairService.InstallAsync(request, downloadDirectory);
        }

        public OperationResult RunCommand(CommandRequest request) => _systemConfigService.Execute(request);
    }
}
