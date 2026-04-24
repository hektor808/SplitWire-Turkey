using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SplitWireTurkey.Services
{
    public sealed class DownloadIntegrityPolicy
    {
        public string ArtifactKey { get; }
        public bool RequireAuthenticodeSignature { get; }
        public IReadOnlyCollection<string> AllowedPublisherSubjects { get; }
        public IReadOnlyDictionary<string, string> Sha256ByVersion { get; }

        public DownloadIntegrityPolicy(
            string artifactKey,
            bool requireAuthenticodeSignature,
            IReadOnlyCollection<string> allowedPublisherSubjects,
            IReadOnlyDictionary<string, string> sha256ByVersion)
        {
            ArtifactKey = artifactKey;
            RequireAuthenticodeSignature = requireAuthenticodeSignature;
            AllowedPublisherSubjects = allowedPublisherSubjects;
            Sha256ByVersion = sha256ByVersion;
        }
    }

    /// <summary>
    /// Uygulamanın ağdan indirdiği dosyalar için sürüm bazlı güvenli hash manifesti.
    /// Yeni sürüm çıktığında ilgili SHA-256 değeri burada güncellenmelidir.
    /// </summary>
    public static class DownloadSecurityManifest
    {
        private static readonly Regex StrictVersionRegex = new(@"^[0-9]+\.[0-9]+\.[0-9]+$", RegexOptions.Compiled);

        private static readonly Dictionary<string, DownloadIntegrityPolicy> Policies =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["wgcf"] = new DownloadIntegrityPolicy(
                    artifactKey: "wgcf",
                    requireAuthenticodeSignature: false,
                    allowedPublisherSubjects: Array.Empty<string>(),
                    sha256ByVersion: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        // Kaynak: https://github.com/ViRb3/wgcf/releases (v2.2.30)
                        // Not: anahtarlar her zaman NormalizeVersionToken sonrası 3 parçalı semver olmalıdır.
                        // Örnek: ["2.2.30"] = "<wgcf_2.2.30_windows_amd64.exe_sha256>"
                    }),
                ["discord_stable"] = new DownloadIntegrityPolicy(
                    artifactKey: "discord_stable",
                    requireAuthenticodeSignature: true,
                    allowedPublisherSubjects: new[]
                    {
                        "CN=Discord Inc."
                    },
                    sha256ByVersion: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        // Örnek: ["1.0.9199"] = "....64-hex-sha256...."
                    }),
                ["discord_ptb"] = new DownloadIntegrityPolicy(
                    artifactKey: "discord_ptb",
                    requireAuthenticodeSignature: true,
                    allowedPublisherSubjects: new[]
                    {
                        "CN=Discord Inc."
                    },
                    sha256ByVersion: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        // Örnek: ["1.0.1100"] = "....64-hex-sha256...."
                    })
            };

        public static string NormalizeVersionToken(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return string.Empty;
            }

            var normalized = version.Trim().TrimStart('v', 'V');
            return StrictVersionRegex.IsMatch(normalized) ? normalized : string.Empty;
        }

        public static bool TryGetPolicy(string artifactKey, out DownloadIntegrityPolicy policy)
        {
            return Policies.TryGetValue(artifactKey, out policy);
        }
    }
}
