using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;

namespace SplitWireTurkey.Services.Security
{
    /// <summary>
    /// SSL sertifika doğrulama politikasını merkezi olarak yönetir.
    /// Gerekirse certificate pinning gibi ileri stratejiler burada genişletilebilir.
    /// </summary>
    public static class CertificatePolicyService
    {
        public static bool IsServerCertificateValid(SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            Debug.WriteLine($"SSL sertifika doğrulaması başarısız: {sslPolicyErrors}");
            return false;
        }

        public static bool IsCertificateValidationFailure(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is AuthenticationException)
                {
                    return true;
                }

                if (current is HttpRequestException httpRequestException &&
                    httpRequestException.Message.IndexOf("certificate", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (current.Message.IndexOf("sertifika", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
