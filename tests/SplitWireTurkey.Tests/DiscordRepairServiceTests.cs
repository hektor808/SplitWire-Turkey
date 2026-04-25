using System;
using System.Reflection;
using SplitWireTurkey.Services.Install;
using Xunit;

namespace SplitWireTurkey.Tests;

public sealed class DiscordRepairServiceTests
{
    [Fact]
    public void IsAllowedPublisherSubject_PolicyHasOnlyCnAndSignerHasAdditionalComponents_Passes()
    {
        var signerSubject = "CN=Discord Inc., O=Discord Inc., L=San Francisco, C=US";
        var policySubject = "CN=Discord Inc.";

        var isAllowed = InvokeIsAllowedPublisherSubject(signerSubject, policySubject);

        Assert.True(isAllowed);
    }

    [Fact]
    public void IsAllowedPublisherSubject_MismatchedCn_Fails()
    {
        var signerSubject = "CN=Discord Inc., O=Discord Inc., L=San Francisco, C=US";
        var policySubject = "CN=Some Other Corp";

        var isAllowed = InvokeIsAllowedPublisherSubject(signerSubject, policySubject);

        Assert.False(isAllowed);
    }

    [Fact]
    public void IsAllowedPublisherSubject_CaseAndSpacingAreNormalized_Passes()
    {
        var signerSubject = "CN=  Discord   Inc.  , O=Discord Inc., C=US";
        var policySubject = " cn = discord inc. ";

        var isAllowed = InvokeIsAllowedPublisherSubject(signerSubject, policySubject);

        Assert.True(isAllowed);
    }

    private static bool InvokeIsAllowedPublisherSubject(string signerSubject, string policySubject)
    {
        var method = typeof(DiscordRepairService)
            .GetMethod("IsAllowedPublisherSubject", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("IsAllowedPublisherSubject bulunamadı.");

        return (bool)(method.Invoke(null, new object[] { signerSubject, policySubject })
            ?? throw new InvalidOperationException("IsAllowedPublisherSubject sonucu alınamadı."));
    }
}
