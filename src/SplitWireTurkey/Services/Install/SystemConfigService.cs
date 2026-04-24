using System;
using System.Diagnostics;
using SplitWireTurkey.Services.InstallContracts;
using SplitWireTurkey.Services.Runtime;

namespace SplitWireTurkey.Services.Install
{
    public class SystemConfigService
    {
        private readonly IFileSystem _fileSystem;
        private readonly IProcessRunner _processRunner;

        public SystemConfigService(IFileSystem? fileSystem = null, IProcessRunner? processRunner = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();
            _processRunner = processRunner ?? new ProcessRunner();
        }

        public string GetAppDataLogsDirectory()
        {
            var appDataPath = _fileSystem.GetLocalApplicationDataPath();
            var logsDirectory = _fileSystem.Combine(appDataPath, "SplitWire-Turkey", "Logs");
            _fileSystem.CreateDirectory(logsDirectory);
            return logsDirectory;
        }

        public string BuildLogPath(string fileName)
        {
            return _fileSystem.Combine(GetAppDataLogsDirectory(), fileName);
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

                var result = _processRunner.Execute(startInfo);
                if (!result.Started)
                {
                    return OperationResult.Fail($"Process başlatılamadı: {request.FileName}");
                }

                if (result.ExitCode == 0)
                {
                    return OperationResult.Ok(string.IsNullOrWhiteSpace(result.StandardOutput)
                        ? "Komut başarılı"
                        : result.StandardOutput.Trim());
                }

                var message = string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardOutput
                    : result.StandardError;

                return OperationResult.Fail(string.IsNullOrWhiteSpace(message)
                    ? $"ExitCode: {result.ExitCode}"
                    : message.Trim());
            }
            catch (Exception ex)
            {
                return OperationResult.Fail(ex.Message);
            }
        }

        public void AppendLog(string path, string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            _fileSystem.AppendAllText(path, line);
        }
    }
}
