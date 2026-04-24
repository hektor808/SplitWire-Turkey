using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SplitWireTurkey.Services
{
    public class UpdateService
    {
        private readonly Action<string> _writeLog;

        public UpdateService(Action<string> writeLog)
        {
            _writeLog = writeLog ?? (_ => { });
        }

        public string GetApplicationVersion()
        {
            try
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Versiyon alma hatası: {ex.Message}");
                return "1.5.2";
            }
        }

        public async Task<string> GetLatestVersionFromGitHubAsync()
        {
            try
            {
                _writeLog("GitHub'dan en son sürüm bilgisi alınıyor...");

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "SplitWire-Turkey");

                var response = await httpClient.GetStringAsync("https://api.github.com/repos/cagritaskn/SplitWire-Turkey/releases/latest");
                _writeLog($"GitHub API Response alındı: {response.Length} karakter");

                var releaseInfo = JsonSerializer.Deserialize<GitHubRelease>(response);
                if (releaseInfo == null)
                {
                    _writeLog("GitHub API response'u null olarak deserialize edildi");
                    return "1.0.0";
                }

                var latestVersion = releaseInfo.TagName?.TrimStart('v') ?? "1.0.0";
                _writeLog($"GitHub'dan alınan en son sürüm: {latestVersion}");
                return latestVersion;
            }
            catch (Exception ex)
            {
                _writeLog($"GitHub'dan sürüm alınırken hata: {ex.Message}");
                Debug.WriteLine($"GitHub'dan sürüm alınırken hata: {ex.Message}");
                return "1.0.0";
            }
        }

        public bool IsNewerVersionAvailable(string currentVersion, string latestVersion)
        {
            try
            {
                _writeLog($"Versiyon karşılaştırması: Mevcut={currentVersion}, En son={latestVersion}");
                var current = Version.Parse(currentVersion);
                var latest = Version.Parse(latestVersion);
                var isNewer = latest > current;
                _writeLog($"Versiyon karşılaştırma sonucu: {(isNewer ? "Yeni sürüm mevcut" : "Güncel sürüm")}");
                return isNewer;
            }
            catch (Exception ex)
            {
                _writeLog($"Versiyon karşılaştırılırken hata: {ex.Message}");
                Debug.WriteLine($"Versiyon karşılaştırılırken hata: {ex.Message}");
                return false;
            }
        }
    }

    public class GitHubRelease
    {
        [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
        public string TagName { get; set; }
    }
}
