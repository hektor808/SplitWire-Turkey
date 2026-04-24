using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SplitWireTurkey.Services;
using SplitWireTurkey.Services.Install;
using Xunit;

namespace SplitWireTurkey.Tests;

public class DiscordRepairServiceTests
{
    [Fact]
    public void ValidateAuthenticodeSignature_WhenSignatureRequiredButMissing_ReturnsFail()
    {
        var policy = CreatePolicy(new[] { "CN=Discord Inc." });

        var result = DiscordRepairService.ValidateAuthenticodeSignature(
            installerPath: "dummy.exe",
            policy: policy,
            signerResolver: _ => null,
            chainValidator: _ => true);

        Assert.False(result.Success);
        Assert.Contains("kod imzası bulunamadı", result.Message);
    }

    [Fact]
    public void ValidateAuthenticodeSignature_WhenPublisherIsNotAllowed_ReturnsFail()
    {
        var policy = CreatePolicy(new[] { "CN=Discord Inc." });
        using var signer = CreateSelfSignedCertificate("CN=Another Publisher");

        var result = DiscordRepairService.ValidateAuthenticodeSignature(
            installerPath: "dummy.exe",
            policy: policy,
            signerResolver: _ => new X509Certificate2(signer),
            chainValidator: _ => true);

        Assert.False(result.Success);
        Assert.Contains("yayıncısı", result.Message);
    }

    [Fact]
    public void ValidateAuthenticodeSignature_WhenPublisherMatchesWithNormalization_ReturnsSuccess()
    {
        var policy = CreatePolicy(new[] { "CN = Discord Inc." });
        using var signer = CreateSelfSignedCertificate("CN=Discord Inc.");

        var result = DiscordRepairService.ValidateAuthenticodeSignature(
            installerPath: "dummy.exe",
            policy: policy,
            signerResolver: _ => new X509Certificate2(signer),
            chainValidator: _ => true);

        Assert.True(result.Success);
    }

    private static DownloadIntegrityPolicy CreatePolicy(IReadOnlyCollection<string> allowedSubjects)
    {
        return new DownloadIntegrityPolicy(
            artifactKey: "discord_stable",
            requireAuthenticodeSignature: true,
            allowedPublisherSubjects: allowedSubjects,
            sha256ByVersion: new Dictionary<string, string>());
    }

    private static X509Certificate2 CreateSelfSignedCertificate(string subject)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(
            notBefore: System.DateTimeOffset.UtcNow.AddDays(-1),
            notAfter: System.DateTimeOffset.UtcNow.AddDays(1));
    }
}
