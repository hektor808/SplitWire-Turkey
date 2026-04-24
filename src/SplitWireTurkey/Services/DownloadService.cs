using System;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading.Tasks;
using SplitWireTurkey.Services.Security;

namespace SplitWireTurkey.Services
{
    public class DownloadService
    {
        public async Task<byte[]> DownloadFileWithRetryAsync(string downloadUrl, string fileName, int maxRetries)
        {
            Exception lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Debug.WriteLine($"{fileName} indirme denemesi {attempt}/{maxRetries} başlatılıyor...");

                    using var httpClient = CreateHttpClientWithAdvancedSettings();
                    httpClient.Timeout = TimeSpan.FromSeconds(45);

                    var setupBytes = await httpClient.GetByteArrayAsync(downloadUrl);
                    if (setupBytes != null && setupBytes.Length > 0)
                    {
                        Debug.WriteLine($"{fileName} başarıyla indirildi. Boyut: {setupBytes.Length} byte");
                        return setupBytes;
                    }

                    throw new Exception("İndirilen dosya boş veya geçersiz");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    var certificateErrorHint = CertificatePolicyService.IsCertificateValidationFailure(ex)
                        ? " (sertifika doğrulama hatası)"
                        : string.Empty;
                    Debug.WriteLine($"{fileName} indirme denemesi {attempt}/{maxRetries} başarısız: {ex.Message}{certificateErrorHint}");

                    if (attempt < maxRetries)
                    {
                        var waitTime = attempt * 3;
                        Debug.WriteLine($"Sonraki deneme öncesi {waitTime} saniye bekleniyor...");
                        await Task.Delay(waitTime * 1000);
                    }
                }
            }

            var certificateFailureReason = lastException != null && CertificatePolicyService.IsCertificateValidationFailure(lastException)
                ? " (sertifika doğrulama hatası)"
                : string.Empty;

            throw new Exception(
                $"{fileName} dosyası {maxRetries} kez denendikten sonra indirilemedi.\n\n" +
                $"Son hata: {lastException?.Message}{certificateFailureReason}\n\n" +
                $"Hata detayı: {lastException}\n\n" +
                $"İndirme URL'i: {downloadUrl}\n\n" +
                "Çözüm önerileri:\n" +
                "• İnternet bağlantınızı kontrol edin\n" +
                "• Güvenlik yazılımınızın Discord'u engellemediğinden emin olun\n" +
                "• Sunucu sertifika doğrulama hatası varsa sistem tarih/saat ve kök sertifikaları kontrol edin\n" +
                "• Proxy veya VPN kullanıyorsanız kapatmayı deneyin\n" +
                "• Windows Defender veya firewall ayarlarını kontrol edin");
        }

        public HttpClient CreateHttpClientWithAdvancedSettings()
        {
            var handler = new HttpClientHandler
            {
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                UseProxy = false,
                Proxy = null,
                MaxConnectionsPerServer = 1,
                MaxAutomaticRedirections = 3,
                UseDefaultCredentials = false,
                ServerCertificateCustomValidationCallback = (_, _, _, sslPolicyErrors) =>
                {
                    return CertificatePolicyService.IsServerCertificateValid(sslPolicyErrors);
                }
            };

            var httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/octet-stream, application/exe, */*");
            return httpClient;
        }
    }
}
