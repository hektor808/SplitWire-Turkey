using System;
using SplitWireTurkey.Services.InstallContracts;

namespace SplitWireTurkey.Services.Install
{
    public class SystemConfigService
    {
        public Func<CommandRequest, OperationResult>? ExecuteHandler { get; set; }
        public Action<string, string>? AppendLogHandler { get; set; }

        public virtual OperationResult Execute(CommandRequest request)
        {
            return ExecuteHandler?.Invoke(request) ?? OperationResult.Ok();
        }

        public virtual void AppendLog(string path, string message)
        {
            AppendLogHandler?.Invoke(path, message);
        }
    }
}
