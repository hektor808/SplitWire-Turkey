using System.Diagnostics;

namespace SplitWireTurkey.Services.Runtime
{
    public interface IProcessRunner
    {
        ProcessExecutionResult Execute(ProcessStartInfo startInfo);
    }

    public sealed record ProcessExecutionResult(bool Started, int ExitCode, string StandardOutput, string StandardError)
    {
        public static ProcessExecutionResult NotStarted() => new(false, -1, string.Empty, string.Empty);
    }

    public sealed class ProcessRunner : IProcessRunner
    {
        public ProcessExecutionResult Execute(ProcessStartInfo startInfo)
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return ProcessExecutionResult.NotStarted();
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return new ProcessExecutionResult(
                Started: true,
                ExitCode: process.ExitCode,
                StandardOutput: output,
                StandardError: error);
        }
    }
}
