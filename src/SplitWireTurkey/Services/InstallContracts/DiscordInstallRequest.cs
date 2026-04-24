namespace SplitWireTurkey.Services.InstallContracts
{
    public record DiscordInstallRequest(bool IsPtb, string InstallerUrl, string InstallerName);
}
