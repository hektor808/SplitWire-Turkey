using System;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Authentication;
using Microsoft.Win32;
using SplitWireTurkey.Services.Security;

namespace SplitWireTurkey.Services.Network
{
    public class HttpClientFactory
    {
        private const string RegistryPath = @"Software\\SplitWire-Turkey";
        private const string RegistryUseProxyKey = "UseProxy";
        private const string EnvironmentUseProxyKey = "SPLITWIRE_USE_PROXY";

        public HttpClient CreateDownloadClient(bool? useProxyOverride = null)
        {
            return CreateClient(
                userAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                acceptHeader: "application/octet-stream, application/exe, */*",
                useProxyOverride: useProxyOverride);
        }

        public HttpClient CreateGitHubApiClient(bool? useProxyOverride = null)
        {
            return CreateClient(
                userAgent: "SplitWire-Turkey",
                acceptHeader: "application/json",
                useProxyOverride: useProxyOverride);
        }

        public static string BuildStandardDownloadFailureMessage(
            string fileName,
            int maxRetries,
            string? lastError,
            bool certificateValidationFailure,
            string? downloadUrl = null,
            string? stackTrace = null)
        {
            var certificateFailureReason = certificateValidationFailure
                ? " (sertifika doğrulama hatası)"
                : string.Empty;

            var errorMessage =
                $"{fileName} dosyası {maxRetries} kez denendikten sonra indirilemedi.\n\n" +
                $"Son hata: {lastError}{certificateFailureReason}\n\n";

            if (!string.IsNullOrWhiteSpace(stackTrace))
            {
                errorMessage += $"Hata detayı: {stackTrace}\n\n";
            }

            if (!string.IsNullOrWhiteSpace(downloadUrl))
            {
                errorMessage += $"İndirme URL'i: {downloadUrl}\n\n";
            }

            errorMessage +=
                "Çözüm önerileri:\n" +
                "• İnternet bağlantınızı kontrol edin\n" +
                "• Sunucu sertifika doğrulama hatası varsa sistem tarih/saat ve kök sertifikaları kontrol edin\n" +
                "• Güvenlik yazılımınızın indirme işlemini engellemediğinden emin olun\n" +
                "• Proxy/firewall kaynaklı engel olabilir: proxy ayarını değiştirip tekrar deneyin\n" +
                "• Windows Defender veya firewall ayarlarını kontrol edin";

            return errorMessage;
        }

        public bool ResolveUseProxy(bool? useProxyOverride = null)
        {
            if (useProxyOverride.HasValue)
            {
                return useProxyOverride.Value;
            }

            var envSetting = Environment.GetEnvironmentVariable(EnvironmentUseProxyKey);
            if (bool.TryParse(envSetting, out var envUseProxy))
            {
                return envUseProxy;
            }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
                var value = key?.GetValue(RegistryUseProxyKey);

                if (value is int intValue)
                {
                    return intValue != 0;
                }

                if (value is string stringValue && bool.TryParse(stringValue, out var boolValue))
                {
                    return boolValue;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Proxy ayarı okunamadı, varsayılan kullanılacak: {ex.Message}");
            }

            return true;
        }

        private HttpClient CreateClient(string userAgent, string acceptHeader, bool? useProxyOverride = null)
        {
            var useProxy = ResolveUseProxy(useProxyOverride);

            var handler = new HttpClientHandler
            {
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                UseProxy = useProxy,
                MaxConnectionsPerServer = 1,
                MaxAutomaticRedirections = 3,
                UseDefaultCredentials = false,
                ServerCertificateCustomValidationCallback = (_, _, _, sslPolicyErrors) =>
                {
                    return CertificatePolicyService.IsServerCertificateValid(sslPolicyErrors);
                }
            };

            var httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
            httpClient.DefaultRequestHeaders.Add("Accept", acceptHeader);
            return httpClient;
        }
    }
}
