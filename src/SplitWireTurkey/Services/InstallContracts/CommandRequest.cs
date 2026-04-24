namespace SplitWireTurkey.Services.InstallContracts
{
    public record CommandRequest(string FileName, string Arguments, bool HiddenWindow = true);
}
