using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Etudiants.Api.Security.Configuration;

public static class MtlsConfiguration
{
    public static void ConfigureMtls(this ConfigureWebHostBuilder webHost, IConfiguration configuration)
    {
        webHost.ConfigureKestrel(options =>
        {
            var certPath = configuration["Kestrel:Endpoints:Https:Certificate:Path"];
            var certPassword = configuration["Kestrel:Endpoints:Https:Certificate:Password"];

            if (string.IsNullOrWhiteSpace(certPath))
                throw new Exception("Certificate path not found.");

            var serverCertificate = new X509Certificate2(certPath, certPassword);

            options.ListenAnyIP(5001, listenOptions =>
            {
                listenOptions.UseHttps(httpsOptions =>
                {
                    httpsOptions.ServerCertificate = serverCertificate;

                    // Require every client to present a certificate
                    httpsOptions.ClientCertificateMode =
                        Microsoft.AspNetCore.Server.Kestrel.Https.ClientCertificateMode.RequireCertificate;

                    httpsOptions.ClientCertificateValidation =
                        (clientCertificate, chain, sslPolicyErrors) =>
                        {
                            if (clientCertificate == null)
                                return false;

                            var caCert = new X509Certificate2(
                                Path.Combine(AppContext.BaseDirectory,
                                    "Security",
                                    "Certificates",
                                    "ca.crt"));

                            var customChain = new X509Chain();

                            customChain.ChainPolicy.TrustMode =
                                X509ChainTrustMode.CustomRootTrust;

                            customChain.ChainPolicy.CustomTrustStore.Add(caCert);

                            customChain.ChainPolicy.RevocationMode =
                                X509RevocationMode.NoCheck;

                            customChain.ChainPolicy.VerificationFlags =
                                X509VerificationFlags.AllowUnknownCertificateAuthority;

                            return customChain.Build(
                                new X509Certificate2(clientCertificate));
                        };
                });
            });
        });
    }
}