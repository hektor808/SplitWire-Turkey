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
        private static readonly Regex Sha256Regex = new(@"^[0-9a-fA-F]{64}$", RegexOptions.Compiled);

        private static readonly Dictionary<string, DownloadIntegrityPolicy> Policies =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["wgcf"] = new DownloadIntegrityPolicy(
                    artifactKey: "wgcf",
                    requireAuthenticodeSignature: false,
                    allowedPublisherSubjects: Array.Empty<string>(),
                    sha256ByVersion: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["2.2.30"] = "A4439DC6CF18CE482CA0D6E264427D6D2CA17237666DCFD64E15FA48E1147DE2"
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
                        ["1.0.9234"] = "3F0306CACEAA9594C608B39DBEE8CEABA4422ABC51052EE21DEDA7280EEE9173"
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
                        ["1.0.1090"] = "44D65372609F2645FE6B52677A81621382A45E5DC014F85BE5400AF42932919A"
                    })
            };

        static DownloadSecurityManifest()
        {
            ValidateManifestNormalization();
        }

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

        private static void ValidateManifestNormalization()
        {
            foreach (var (artifactKey, policy) in Policies)
            {
                foreach (var (versionKey, sha256) in policy.Sha256ByVersion)
                {
                    var normalized = NormalizeVersionToken(versionKey);
                    if (!string.Equals(normalized, versionKey, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"{artifactKey} manifest sürüm anahtarı NormalizeVersionToken ile birebir eşleşmiyor: {versionKey}");
                    }

                    if (!Sha256Regex.IsMatch(sha256))
                    {
                        throw new InvalidOperationException(
                            $"{artifactKey} {versionKey} için SHA-256 değeri geçersiz: {sha256}");
                    }
                }
            }
        }
    }
}
