namespace SplitWireTurkey.Services.InstallContracts
{
    public record ServiceInstallRequest(string ServiceName, string Parameters, string? LogPath = null);
}
