using SplitWireTurkey.Services;
using Xunit;

namespace SplitWireTurkey.Tests;

public sealed class DpiBypassProfileServiceTests
{
    [Fact]
    public void ExtractZapretParametersFromSummary_ReturnsTailParameters()
    {
        var service = new DpiBypassProfileService();
        var log = "prefix\n* SUMMARY\nline\ncmd --wf-tcp=443 --hostlist=/tmp/list.txt --dpi-desync=fake\n";

        var result = service.ExtractZapretParametersFromSummary(log);

        Assert.Equal("--hostlist=/tmp/list.txt --dpi-desync=fake", result);
    }

    [Fact]
    public void BuildZapretServiceParameters_PrependsRequiredPorts()
    {
        var service = new DpiBypassProfileService();

        var result = service.BuildZapretServiceParameters("--dpi-desync=fake");

        Assert.Equal("--wf-tcp=80,443 --wf-udp=443,50000,50100 --dpi-desync=fake", result);
    }
}
