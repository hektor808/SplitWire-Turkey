using System;
using System.Threading.Tasks;

namespace SplitWireTurkey.Services
{
    public interface IRepairService
    {
        Task CloseWebCordProcessesAsync();
        Task CloseDiscordProcessesAsync();
        Task UninstallDiscordAsync();
        Task UninstallDiscordPTBAsync();
        Task InstallDiscordAsync();
        Task InstallDiscordPTBAsync();
    }

    public class RepairServiceAdapter : IRepairService
    {
        private readonly Func<Task> _closeWebCord;
        private readonly Func<Task> _closeDiscord;
        private readonly Func<Task> _uninstallDiscord;
        private readonly Func<Task> _uninstallDiscordPtb;
        private readonly Func<Task> _installDiscord;
        private readonly Func<Task> _installDiscordPtb;

        public RepairServiceAdapter(
            Func<Task> closeWebCord,
            Func<Task> closeDiscord,
            Func<Task> uninstallDiscord,
            Func<Task> uninstallDiscordPtb,
            Func<Task> installDiscord,
            Func<Task> installDiscordPtb)
        {
            _closeWebCord = closeWebCord;
            _closeDiscord = closeDiscord;
            _uninstallDiscord = uninstallDiscord;
            _uninstallDiscordPtb = uninstallDiscordPtb;
            _installDiscord = installDiscord;
            _installDiscordPtb = installDiscordPtb;
        }

        public Task CloseWebCordProcessesAsync() => _closeWebCord();
        public Task CloseDiscordProcessesAsync() => _closeDiscord();
        public Task UninstallDiscordAsync() => _uninstallDiscord();
        public Task UninstallDiscordPTBAsync() => _uninstallDiscordPtb();
        public Task InstallDiscordAsync() => _installDiscord();
        public Task InstallDiscordPTBAsync() => _installDiscordPtb();
    }
}
