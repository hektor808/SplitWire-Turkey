using System;
using System.Linq;

namespace SplitWireTurkey.Services
{
    public class DpiBypassProfileService
    {
        public string ExtractZapretParametersFromSummary(string logContent)
        {
            var summaryIndex = logContent.IndexOf("* SUMMARY", StringComparison.Ordinal);
            if (summaryIndex == -1)
            {
                return null;
            }

            var lines = logContent.Substring(summaryIndex).Split('\n');
            foreach (var line in lines.Skip(1))
            {
                if (!line.Contains("--wf-tcp=443", StringComparison.Ordinal))
                {
                    continue;
                }

                var tcpIndex = line.IndexOf("--wf-tcp=443", StringComparison.Ordinal);
                if (tcpIndex >= 0)
                {
                    return line.Substring(tcpIndex + "--wf-tcp=443".Length).Trim();
                }
            }

            return null;
        }

        public string BuildZapretServiceParameters(string extractedParameters)
        {
            return $"--wf-tcp=80,443 --wf-udp=443,50000,50100 {extractedParameters}";
        }
    }
}
