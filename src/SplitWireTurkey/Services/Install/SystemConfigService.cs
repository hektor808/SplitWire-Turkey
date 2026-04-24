using System;
using System.Diagnostics;
using System.IO;
using SplitWireTurkey.Services.InstallContracts;

namespace SplitWireTurkey.Services.Install
{
    public class SystemConfigService
    {
        public string GetAppDataLogsDirectory()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logsDirectory = Path.Combine(appDataPath, "SplitWire-Turkey", "Logs");
            Directory.CreateDirectory(logsDirectory);
            return logsDirectory;
        }

        public string BuildLogPath(string fileName)
        {
            return Path.Combine(GetAppDataLogsDirectory(), fileName);
        }

        public OperationResult Execute(CommandRequest request)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = request.FileName,
                    Arguments = request.Arguments,
                    CreateNoWindow = request.HiddenWindow,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return OperationResult.Fail($"Process başlatılamadı: {request.FileName}");
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    return OperationResult.Ok(string.IsNullOrWhiteSpace(output) ? "Komut başarılı" : output.Trim());
                }

                var message = string.IsNullOrWhiteSpace(error) ? output : error;
                return OperationResult.Fail(string.IsNullOrWhiteSpace(message) ? $"ExitCode: {process.ExitCode}" : message.Trim());
            }
            catch (Exception ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }

        public void AppendLog(string path, string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(path, line);
        }
    }
}
