using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SplitWireTurkey.Services.InstallContracts;

namespace SplitWireTurkey.Services.Install
{
    public class DiscordRepairService
    {
        private static readonly Regex DiscordVersionRegex = new(@"(\d+\.\d+\.\d+)", RegexOptions.Compiled);
        private readonly SystemConfigService _systemConfigService;

        public DiscordRepairService(SystemConfigService systemConfigService)
        {
            _systemConfigService = systemConfigService;
        }

        public OperationResult CloseProcess(string processName)
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    process.Kill();
                }

                return OperationResult.Ok($"{processName} kapatıldı");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }

        public OperationResult Uninstall(string updaterPath, string uninstallArgs)
        {
            return _systemConfigService.Execute(new CommandRequest(updaterPath, uninstallArgs));
        }

        public async Task<OperationResult> InstallAsync(DiscordInstallRequest request, string downloadDirectory)
        {
            try
            {
                Directory.CreateDirectory(downloadDirectory);
                var installerPath = Path.Combine(downloadDirectory, request.InstallerName);
                var artifactKey = request.IsPtb ? "discord_ptb" : "discord_stable";

                if (!DownloadSecurityManifest.TryGetPolicy(artifactKey, out var policy))
                {
                    return OperationResult.Fail($"{artifactKey} güvenlik politikası bulunamadı.");
                }

                using var client = new HttpClient();
                await using (var output = File.Create(installerPath))
                await using (var stream = await client.GetStreamAsync(request.InstallerUrl))
                {
                    await stream.CopyToAsync(output);
                }

                var version = await ResolveInstallerVersionAsync(client, request, installerPath);
                if (!DownloadSecurityManifest.TryGetExpectedHash(
                        policy,
                        version,
                        out var expectedHash,
                        out var hashLookupError,
                        logSignal: message => Debug.WriteLine($"TELEMETRY_MANIFEST {message}")))
                {
                    return OperationResult.Fail(hashLookupError);
                }

                var actualHash = ComputeSha256(installerPath);
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResult.Fail(
                        $"{artifactKey} {version} SHA-256 uyuşmuyor. Beklenen: {expectedHash}, Gelen: {actualHash}");
                }

                if (policy.RequireAuthenticodeSignature)
                {
                    var signatureValidation = ValidateAuthenticodeSignature(installerPath, policy);
                    if (!signatureValidation.Success)
                    {
                        return signatureValidation;
                    }
                }

                return _systemConfigService.Execute(new CommandRequest(installerPath, string.Empty));
            }
            catch (Exception ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }

        private static async Task<string> ResolveInstallerVersionAsync(HttpClient client, DiscordInstallRequest request, string installerPath)
        {
            try
            {
                var finalUri = (await client.GetAsync(request.InstallerUrl, HttpCompletionOption.ResponseHeadersRead)).RequestMessage?.RequestUri;
                var fromUri = DownloadSecurityManifest.NormalizeVersionToken(ExtractVersion(finalUri?.ToString()));
                if (!string.IsNullOrWhiteSpace(fromUri))
                {
                    return fromUri;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Discord installer final URL version alınamadı: {ex.Message}");
            }

            var fromName = DownloadSecurityManifest.NormalizeVersionToken(ExtractVersion(request.InstallerName));
            if (!string.IsNullOrWhiteSpace(fromName))
            {
                return fromName;
            }

            var fromFile = DownloadSecurityManifest.NormalizeVersionToken(
                ExtractVersion(FileVersionInfo.GetVersionInfo(installerPath).FileVersion));
            return fromFile;
        }

        private static string ExtractVersion(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var match = DiscordVersionRegex.Match(text);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static string ComputeSha256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexString(sha256.ComputeHash(stream));
        }

        internal static OperationResult ValidateAuthenticodeSignature(
            string installerPath,
            DownloadIntegrityPolicy policy,
            Func<string, X509Certificate2?> signerResolver = null,
            Func<X509Certificate2, bool> chainValidator = null)
        {
            signerResolver ??= DefaultSignerResolver;
            chainValidator ??= DefaultChainValidator;

            X509Certificate2? signerCertificate;
            try
            {
                signerCertificate = signerResolver(installerPath);
            }
            catch (CryptographicException ex)
            {
                Debug.WriteLine($"Authenticode imza okunamadı: {ex.Message}");
                return OperationResult.Fail("Kurulum dosyasında kod imzası bulunamadı.");
            }

            if (signerCertificate is null)
            {
                Debug.WriteLine("Authenticode doğrulaması başarısız: imza yok.");
                return OperationResult.Fail("Kurulum dosyasında kod imzası bulunamadı.");
            }

            using (signerCertificate)
            {
                if (!chainValidator(signerCertificate))
                {
                    Debug.WriteLine($"Authenticode doğrulaması başarısız: zincir geçersiz. Subject={signerCertificate.Subject}");
                    return OperationResult.Fail("Kurulum dosyasının kod imza zinciri doğrulanamadı.");
                }

                if (!IsAllowedPublisherSubject(signerCertificate.Subject, policy.AllowedPublisherSubjects))
                {
                    Debug.WriteLine(
                        $"Authenticode doğrulaması başarısız: publisher uyuşmazlığı. Subject={signerCertificate.Subject}");
                    return OperationResult.Fail("Kurulum dosyasının kod imza yayıncısı güvenlik politikası ile eşleşmiyor.");
                }
            }

            return OperationResult.Ok("Authenticode imza doğrulaması başarılı.");
        }

        private static X509Certificate2? DefaultSignerResolver(string installerPath)
        {
            var certificate = X509Certificate.CreateFromSignedFile(installerPath);
            return certificate is null ? null : new X509Certificate2(certificate);
        }

        private static bool DefaultChainValidator(X509Certificate2 signerCertificate)
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            return chain.Build(signerCertificate);
        }

        private static bool IsAllowedPublisherSubject(string signerSubject, System.Collections.Generic.IReadOnlyCollection<string> allowedSubjects)
        {
            if (string.IsNullOrWhiteSpace(signerSubject) || allowedSubjects is null || allowedSubjects.Count == 0)
            {
                return false;
            }

            foreach (var allowed in allowedSubjects)
            {
                if (string.Equals(signerSubject, allowed, StringComparison.Ordinal))
                {
                    return true;
                }

                if (string.Equals(NormalizeSubject(signerSubject), NormalizeSubject(allowed), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeSubject(string subject)
        {
            if (string.IsNullOrWhiteSpace(subject))
            {
                return string.Empty;
            }

            return Regex.Replace(subject, @"\s+", string.Empty).ToUpperInvariant();
        }
    }
}
