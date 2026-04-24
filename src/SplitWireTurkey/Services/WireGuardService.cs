using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.Generic; // Added missing import
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SplitWireTurkey.Services
{
    public class WireGuardService
    {
        private class CommandExecutionResult
        {
            public int ExitCode { get; init; }
            public string StandardOutput { get; init; } = string.Empty;
            public string StandardError { get; init; } = string.Empty;
            public string ExceptionMessage { get; init; } = string.Empty;
        }

        private class WgcfErrorModel
        {
            public string ErrorCode { get; init; } = string.Empty;
            public string UserMessage { get; init; } = string.Empty;
            public string DebugDetail { get; init; } = string.Empty;
        }

        private readonly string _wgcfPath;
        private readonly string _wgcfVersionPath;
        private readonly string _resDir;

        public WireGuardService()
        {
            _resDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "res");
            _wgcfPath = Path.Combine(_resDir, "wgcf.exe");
            _wgcfVersionPath = Path.Combine(_resDir, "wgcf.version");
            
            if (!Directory.Exists(_resDir))
                Directory.CreateDirectory(_resDir);
        }

        public async Task<bool> CreateProfileAsync(string[] extraFolders = null, bool includeBrowsers = false)
        {
            try
            {
                var wgcfPath = await DownloadWgcfAsync();
                if (string.IsNullOrEmpty(wgcfPath))
                {
                    HandleWgcfFailure(new WgcfErrorModel
                    {
                        ErrorCode = "WGCF_DOWNLOAD_FAILED",
                        UserMessage = LanguageManager.GetText("messages", "wgcf_download_failed"),
                        DebugDetail = "DownloadWgcfAsync returned an empty path."
                    });
                    return false;
                }

                // Remove existing files if they exist to avoid validating stale output.
                var accountFile = Path.Combine(_resDir, "wgcf-account.toml");
                var profileFile = Path.Combine(_resDir, "wgcf-profile.conf");
                if (File.Exists(accountFile))
                {
                    try { File.Delete(accountFile); } catch { }
                }
                if (File.Exists(profileFile))
                {
                    try { File.Delete(profileFile); } catch { }
                }

                // Register with wgcf
                var registerResult = await ExecuteCommandAsync(_wgcfPath, "register --accept-tos");
                var registerFileValid = IsValidAccountFile(out var registerValidationDetail);
                if (registerResult.ExitCode == 0 && !registerFileValid)
                {
                    HandleWgcfFailure(new WgcfErrorModel
                    {
                        ErrorCode = "WGCF_REGISTER_FILE_INVALID",
                        UserMessage = string.Format(LanguageManager.GetText("messages", "profile_creation_error"), "register validation failed"),
                        DebugDetail = $"register exit=0 but account validation failed. {registerValidationDetail}"
                    });
                    return false;
                }

                if (registerResult.ExitCode != 0)
                {
                    var safeFallback = registerFileValid && IsSafeRegisterFallback(registerResult.StandardOutput, registerResult.StandardError);
                    if (!safeFallback)
                    {
                        HandleWgcfFailure(new WgcfErrorModel
                        {
                            ErrorCode = "WGCF_REGISTER_FAILED",
                            UserMessage = string.Format(LanguageManager.GetText("messages", "profile_creation_error"), "register command failed"),
                            DebugDetail = $"register exit={registerResult.ExitCode}; validation={registerValidationDetail}; stderr={registerResult.StandardError}; stdout={registerResult.StandardOutput}"
                        });
                        return false;
                    }

                    Debug.WriteLine($"Register fallback accepted. exit={registerResult.ExitCode}; detail={registerValidationDetail}");
                }

                // Generate profile
                var generateResult = await ExecuteCommandAsync(_wgcfPath, "generate");
                var generateFileValid = IsValidProfileFile(out var generateValidationDetail);

                if (generateResult.ExitCode == 0 && !generateFileValid)
                {
                    HandleWgcfFailure(new WgcfErrorModel
                    {
                        ErrorCode = "WGCF_GENERATE_FILE_INVALID",
                        UserMessage = string.Format(LanguageManager.GetText("messages", "profile_creation_error"), "generate validation failed"),
                        DebugDetail = $"generate exit=0 but profile validation failed. {generateValidationDetail}"
                    });
                    return false;
                }

                if (generateResult.ExitCode != 0)
                {
                    var safeFallback = generateFileValid && IsSafeGenerateFallback(generateResult.StandardOutput, generateResult.StandardError);
                    if (!safeFallback)
                    {
                        HandleWgcfFailure(new WgcfErrorModel
                        {
                            ErrorCode = "WGCF_GENERATE_FAILED",
                            UserMessage = string.Format(LanguageManager.GetText("messages", "profile_creation_error"), "generate command failed"),
                            DebugDetail = $"generate exit={generateResult.ExitCode}; validation={generateValidationDetail}; stderr={generateResult.StandardError}; stdout={generateResult.StandardOutput}"
                        });
                        return false;
                    }

                    Debug.WriteLine($"Generate fallback accepted. exit={generateResult.ExitCode}; detail={generateValidationDetail}");
                }

                // Modify configuration
                var profilePath = Path.Combine(_resDir, "wgcf-profile.conf");
                if (File.Exists(profilePath) && IsValidProfileFile(out _))
                {
                    return await ModifyConfigurationAsync(profilePath, extraFolders, includeBrowsers);
                }
                else
                {
                    HandleWgcfFailure(new WgcfErrorModel
                    {
                        ErrorCode = "WGCF_PROFILE_NOT_FOUND",
                        UserMessage = LanguageManager.GetText("messages", "profile_not_found"),
                        DebugDetail = "Profile file is missing or invalid after generate stage."
                    });
                    return false;
                }
            }
            catch (Exception ex)
            {
                HandleWgcfFailure(new WgcfErrorModel
                {
                    ErrorCode = "WGCF_PROFILE_CREATE_EXCEPTION",
                    UserMessage = string.Format(LanguageManager.GetText("messages", "profile_creation_error"), ex.Message),
                    DebugDetail = ex.ToString()
                });
                return false;
            }
        }

        private async Task<bool> ModifyConfigurationAsync(string profilePath, string[] extraFolders, bool includeBrowsers = false)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(profilePath);
                var newLines = new List<string>();
                var username = Environment.UserName;
                var discordPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Discord");

                var appPaths = new List<string>
                {
                    discordPath,
                    "discord",
                    "roblox",
                    "Discord.exe",
                    "DiscordPTB.exe",
                    "webcord.exe",
                    "SplitWire-Turkey.exe",
                    "Update.exe",
                    "RobloxPlayerBeta.exe",
                    "RobloxPlayerInstaller.exe"
                };

                // Tarayıcı uygulamalarını ekle (eğer isteniyorsa)
                if (includeBrowsers)
                {
                    var browserApps = new[]
                    {
                        "browser.exe",
                        "chrome.exe",
                        "firefox.exe",
                        "opera.exe",
                        "operagx.exe",
                        "brave.exe",
                        "vivaldi.exe",
                        "msedge.exe",
                        "zen.exe",
                        "chromium.exe",
                        "iexplore.exe",
                        "Maxthon.exe",
                        "librewolf.exe",
                        "electron.exe"
                    };
                    appPaths.AddRange(browserApps);
                }

                if (extraFolders != null)
                {
                    foreach (var folder in extraFolders)
                    {
                        if (!string.IsNullOrWhiteSpace(folder))
                            appPaths.Add(folder.Trim());
                    }
                }

                var allowedAppsLine = $"AllowedApps = {string.Join(", ", appPaths)}";

                foreach (var line in lines)
                {
                    newLines.Add(line);
                    if (line.Trim().StartsWith("Endpoint"))
                    {
                        newLines.Add(allowedAppsLine);
                    }
                }

                await File.WriteAllLinesAsync(profilePath, newLines);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LanguageManager.GetText("messages", "config_edit_error"), ex.Message), 
                    LanguageManager.GetText("messages", "unexpected_error_title"), MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private async Task<CommandExecutionResult> ExecuteCommandAsync(string command, string arguments)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = arguments,
                        WorkingDirectory = _resDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = new Process { StartInfo = startInfo };
                    process.Start();
                    
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    
                    // Log the command execution details
                    Debug.WriteLine($"Command: {command} {arguments}");
                    Debug.WriteLine($"Working Directory: {_resDir}");
                    Debug.WriteLine($"Exit Code: {process.ExitCode}");
                    Debug.WriteLine($"Output: {output}");
                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.WriteLine($"Error: {error}");
                    }
                    return new CommandExecutionResult
                    {
                        ExitCode = process.ExitCode,
                        StandardOutput = output,
                        StandardError = error
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Command execution failed: {ex.Message}");
                    return new CommandExecutionResult
                    {
                        ExitCode = -1,
                        ExceptionMessage = ex.ToString(),
                        StandardError = ex.Message
                    };
                }
            });
        }

        public string GetConfigPath()
        {
            return Path.Combine(_resDir, "wgcf-profile.conf");
        }

        public bool CheckWgcfFilesExist()
        {
            var accountIsValid = IsValidAccountFile(out var accountDetail);
            var profileIsValid = IsValidProfileFile(out var profileDetail);

            Debug.WriteLine($"wgcf-account.toml valid: {accountIsValid}. Detail: {accountDetail}");
            Debug.WriteLine($"wgcf-profile.conf valid: {profileIsValid}. Detail: {profileDetail}");

            return accountIsValid && profileIsValid;
        }

        private void HandleWgcfFailure(WgcfErrorModel error)
        {
            Debug.WriteLine($"WGCF_ERROR[{error.ErrorCode}] {error.DebugDetail}");
            MessageBox.Show(error.UserMessage, LanguageManager.GetText("messages", "unexpected_error_title"), MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private bool IsValidAccountFile(out string detail)
        {
            var accountFile = Path.Combine(_resDir, "wgcf-account.toml");
            if (!File.Exists(accountFile))
            {
                detail = "wgcf-account.toml does not exist.";
                return false;
            }

            var content = File.ReadAllText(accountFile);
            if (string.IsNullOrWhiteSpace(content))
            {
                detail = "wgcf-account.toml is empty.";
                return false;
            }

            var hasLicenseKey = content.IndexOf("license_key", StringComparison.OrdinalIgnoreCase) >= 0;
            var hasPrivateKey = content.IndexOf("private_key", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!hasLicenseKey || !hasPrivateKey)
            {
                detail = $"Missing required account fields. license_key={hasLicenseKey}, private_key={hasPrivateKey}.";
                return false;
            }

            detail = "wgcf-account.toml passed validation.";
            return true;
        }

        private bool IsValidProfileFile(out string detail)
        {
            var profileFile = Path.Combine(_resDir, "wgcf-profile.conf");
            if (!File.Exists(profileFile))
            {
                detail = "wgcf-profile.conf does not exist.";
                return false;
            }

            var content = File.ReadAllText(profileFile);
            if (string.IsNullOrWhiteSpace(content))
            {
                detail = "wgcf-profile.conf is empty.";
                return false;
            }

            var hasInterface = content.IndexOf("[Interface]", StringComparison.OrdinalIgnoreCase) >= 0;
            var hasPeer = content.IndexOf("[Peer]", StringComparison.OrdinalIgnoreCase) >= 0;
            var hasEndpoint = content.IndexOf("Endpoint", StringComparison.OrdinalIgnoreCase) >= 0;
            var hasPrivateKey = content.IndexOf("PrivateKey", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!hasInterface || !hasPeer || !hasEndpoint || !hasPrivateKey)
            {
                detail = $"Missing required profile sections/fields. interface={hasInterface}, peer={hasPeer}, endpoint={hasEndpoint}, privateKey={hasPrivateKey}.";
                return false;
            }

            detail = "wgcf-profile.conf passed validation.";
            return true;
        }

        private bool IsSafeRegisterFallback(string standardOutput, string standardError)
        {
            var combined = $"{standardOutput}\n{standardError}";
            return combined.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("exists", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsSafeGenerateFallback(string standardOutput, string standardError)
        {
            var combined = $"{standardOutput}\n{standardError}";
            return combined.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("exists", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private async Task<string> DownloadWgcfAsync()
        {
            try
            {
                if (!DownloadSecurityManifest.TryGetPolicy("wgcf", out var policy))
                {
                    throw new InvalidOperationException("wgcf güvenlik politikası bulunamadı.");
                }

                // Check if wgcf.exe already exists and is not too old (7 days)
                if (File.Exists(_wgcfPath))
                {
                    var fileInfo = new FileInfo(_wgcfPath);
                    if (DateTime.Now.Subtract(fileInfo.CreationTime).TotalDays < 7)
                    {
                        var cachedVersion = File.Exists(_wgcfVersionPath)
                            ? (await File.ReadAllTextAsync(_wgcfVersionPath)).Trim()
                            : string.Empty;

                        if (ValidateFileHash(_wgcfPath, cachedVersion, policy, out var cachedReason))
                        {
                            Debug.WriteLine("Using existing wgcf.exe (less than 7 days old and integrity verified)");
                            return _wgcfPath;
                        }

                        Debug.WriteLine($"Cached wgcf.exe integrity check failed: {cachedReason}");
                        TryDeleteFile(_wgcfPath);
                        TryDeleteFile(_wgcfVersionPath);
                    }
                }

                // Download latest release info from GitHub
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "SplitWire-Turkey");
                    
                    var releasesUrl = "https://api.github.com/repos/ViRb3/wgcf/releases/latest";
                    var releasesResponse = await client.GetStringAsync(releasesUrl);

                    using var jsonDoc = JsonDocument.Parse(releasesResponse);
                    var root = jsonDoc.RootElement;
                    var releaseVersion = root.TryGetProperty("tag_name", out var tagNameElement)
                        ? tagNameElement.GetString()?.TrimStart('v') ?? string.Empty
                        : string.Empty;

                    string downloadUrl = null;
                    if (root.TryGetProperty("assets", out var assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var asset in assetsElement.EnumerateArray())
                        {
                            var assetName = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : string.Empty;
                            var assetUrl = asset.TryGetProperty("browser_download_url", out var urlElement) ? urlElement.GetString() : string.Empty;

                            if (!string.IsNullOrWhiteSpace(assetName) &&
                                !string.IsNullOrWhiteSpace(assetUrl) &&
                                assetName.Contains("windows_amd64", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = assetUrl;
                                releaseVersion = ExtractWgcfVersionFromAssetName(assetName, releaseVersion);
                                break;
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(downloadUrl))
                    {
                        Debug.WriteLine("Windows AMD64 version not found in GitHub releases");
                        System.Windows.MessageBox.Show(LanguageManager.GetText("messages", "wgcf_version_not_found"),
                            LanguageManager.GetText("messages", "unexpected_error_title"), MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }

                    var response = await client.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();
                    var binary = await response.Content.ReadAsByteArrayAsync();

                    var tempPath = $"{_wgcfPath}.download";
                    await File.WriteAllBytesAsync(tempPath, binary);
                    try
                    {
                        if (!ValidateFileHash(tempPath, releaseVersion, policy, out var hashError))
                        {
                            throw new InvalidOperationException(hashError);
                        }

                        if (policy.RequireAuthenticodeSignature &&
                            !VerifyAuthenticodeSignature(tempPath, policy.AllowedPublisherSubjects, out var signError))
                        {
                            throw new InvalidOperationException(signError);
                        }

                        File.Copy(tempPath, _wgcfPath, true);
                        await File.WriteAllTextAsync(_wgcfVersionPath, releaseVersion);
                    }
                    finally
                    {
                        TryDeleteFile(tempPath);
                    }
                }

                return _wgcfPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"wgcf.exe download failed: {ex.Message}");
                
                // Try to use existing file if available
                if (File.Exists(_wgcfPath))
                {
                    Debug.WriteLine("Existing wgcf.exe found after download failure, integrity will be re-checked");
                    if (DownloadSecurityManifest.TryGetPolicy("wgcf", out var fallbackPolicy))
                    {
                        var cachedVersion = File.Exists(_wgcfVersionPath)
                            ? File.ReadAllText(_wgcfVersionPath).Trim()
                            : string.Empty;
                        if (ValidateFileHash(_wgcfPath, cachedVersion, fallbackPolicy, out _))
                        {
                            return _wgcfPath;
                        }
                    }

                    TryDeleteFile(_wgcfPath);
                    TryDeleteFile(_wgcfVersionPath);
                }

                System.Windows.MessageBox.Show(string.Format(LanguageManager.GetText("messages", "wgcf_download_error"), ex.Message),
                    LanguageManager.GetText("messages", "unexpected_error_title"), MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private static string ExtractWgcfVersionFromAssetName(string assetName, string fallbackVersion)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return fallbackVersion;
            }

            var match = Regex.Match(assetName, @"wgcf_v?([0-9]+\.[0-9]+\.[0-9]+)_", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : fallbackVersion;
        }

        private static bool ValidateFileHash(string filePath, string version, DownloadIntegrityPolicy policy, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(version))
            {
                errorMessage = $"{policy.ArtifactKey} için sürüm bilgisi alınamadı; indirme güvenlik doğrulaması durduruldu.";
                return false;
            }

            if (!policy.Sha256ByVersion.TryGetValue(version, out var expectedHash))
            {
                errorMessage = $"{policy.ArtifactKey} {version} sürümü için manifestte SHA-256 değeri bulunamadı. Kurulum engellendi.";
                return false;
            }

            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var actualHash = Convert.ToHexString(sha256.ComputeHash(stream));
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"{policy.ArtifactKey} {version} SHA-256 uyuşmuyor. Beklenen: {expectedHash}, Gelen: {actualHash}";
                return false;
            }

            return true;
        }

        private static bool VerifyAuthenticodeSignature(string filePath, IReadOnlyCollection<string> allowedPublisherSubjects, out string errorMessage)
        {
            errorMessage = null;
            if (!allowedPublisherSubjects.Any())
            {
                return true;
            }

            try
            {
                var certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                    System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(filePath));

                var publisherAllowed = allowedPublisherSubjects.Any(subject =>
                    certificate.Subject.Contains(subject, StringComparison.OrdinalIgnoreCase));
                if (!publisherAllowed)
                {
                    errorMessage = $"İmza geçersiz: beklenmeyen yayıncı ({certificate.Subject}).";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"İmza doğrulanamadı: {ex.Message}";
                return false;
            }
        }

        private static void TryDeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
                // no-op
            }
        }
    }
}
