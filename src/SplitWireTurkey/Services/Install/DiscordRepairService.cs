using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
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
    }
}
