using System;

namespace SplitWireTurkey.Services
{
    public static class VersionComparisonService
    {
        public static bool IsNewerVersion(string currentVersion, string latestVersion)
        {
            if (string.IsNullOrWhiteSpace(currentVersion) || string.IsNullOrWhiteSpace(latestVersion))
            {
                return false;
            }

            if (!Version.TryParse(currentVersion, out var current))
            {
                return false;
            }

            if (!Version.TryParse(latestVersion, out var latest))
            {
                return false;
            }

            return latest > current;
        }
    }
}
