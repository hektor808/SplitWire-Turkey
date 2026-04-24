using System;
using System.Diagnostics;
using System.Threading.Tasks;
using SplitWireTurkey.Services.Network;
using SplitWireTurkey.Services.Security;

namespace SplitWireTurkey.Services
{
    public class DownloadService
    {
        private readonly HttpClientFactory _httpClientFactory;

        public DownloadService(HttpClientFactory? httpClientFactory = null)
        {
            _httpClientFactory = httpClientFactory ?? new HttpClientFactory();
        }

        public async Task<byte[]> DownloadFileWithRetryAsync(string downloadUrl, string fileName, int maxRetries)
        {
            Exception lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Debug.WriteLine($"{fileName} indirme denemesi {attempt}/{maxRetries} başlatılıyor...");

                    using var httpClient = _httpClientFactory.CreateDownloadClient();
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

            throw new Exception(HttpClientFactory.BuildStandardDownloadFailureMessage(
                fileName,
                maxRetries,
                lastException?.Message,
                lastException != null && CertificatePolicyService.IsCertificateValidationFailure(lastException),
                downloadUrl,
                lastException?.ToString()));
        }
    }
}
