using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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

        private static bool IsAllowedPublisherSubject(string signerSubject, string policySubject)
        {
            if (string.IsNullOrWhiteSpace(signerSubject) || string.IsNullOrWhiteSpace(policySubject))
            {
                return false;
            }

            if (string.Equals(signerSubject.Trim(), policySubject.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var signerComponents = ParseDistinguishedNameComponents(signerSubject);
            var policyComponents = ParseDistinguishedNameComponents(policySubject);
            if (signerComponents.Count == 0 || policyComponents.Count == 0)
            {
                return false;
            }

            return policyComponents.All(policyComponent =>
                signerComponents.TryGetValue(policyComponent.Key, out var signerValues) &&
                signerValues.Contains(policyComponent.Value, StringComparer.OrdinalIgnoreCase));
        }

        private static Dictionary<string, List<string>> ParseDistinguishedNameComponents(string distinguishedName)
        {
            var components = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(distinguishedName))
            {
                return components;
            }

            var tokens = TryFormatDistinguishedName(distinguishedName)
                ?? TokenizeDistinguishedName(distinguishedName);

            foreach (var token in tokens)
            {
                var separatorIndex = token.IndexOf('=');
                if (separatorIndex <= 0 || separatorIndex == token.Length - 1)
                {
                    continue;
                }

                var key = NormalizeDistinguishedNameComponent(token[..separatorIndex]);
                var value = NormalizeDistinguishedNameComponent(token[(separatorIndex + 1)..]);
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                if (!components.TryGetValue(key, out var values))
                {
                    values = new List<string>();
                    components[key] = values;
                }

                if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    values.Add(value);
                }
            }

            return components;
        }

        private static IEnumerable<string>? TryFormatDistinguishedName(string distinguishedName)
        {
            try
            {
                var x500Name = new X500DistinguishedName(distinguishedName);
                var formatted = x500Name.Format(true);
                return formatted
                    .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(entry => entry.Trim());
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        private static IEnumerable<string> TokenizeDistinguishedName(string distinguishedName)
        {
            var tokens = new List<string>();
            var startIndex = 0;
            var escapeNext = false;

            for (var index = 0; index < distinguishedName.Length; index++)
            {
                var current = distinguishedName[index];
                if (escapeNext)
                {
                    escapeNext = false;
                    continue;
                }

                if (current == '\\')
                {
                    escapeNext = true;
                    continue;
                }

                if (current != ',')
                {
                    continue;
                }

                tokens.Add(distinguishedName[startIndex..index].Trim());
                startIndex = index + 1;
            }

            if (startIndex < distinguishedName.Length)
            {
                tokens.Add(distinguishedName[startIndex..].Trim());
            }

            return tokens;
        }

        private static string NormalizeDistinguishedNameComponent(string component)
        {
            return Regex.Replace(component.Trim(), "\\s+", " ");
        }
    }
}
