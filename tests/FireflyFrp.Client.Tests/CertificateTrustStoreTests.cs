using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using FireflyFrp.Client.Services;

namespace FireflyFrp.Client.Tests;

public sealed class CertificateTrustStoreTests
{
    [Fact]
    public void ConfiguredFingerprintIsTrustedWithoutPrompt()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));
        var fingerprint = CertificateTrustStore.ComputeSha256(certificate);

        var configPath = Path.Combine(AppPaths.BaseDirectory, "client.config.json");
        var backup = File.Exists(configPath) ? File.ReadAllText(configPath) : null;
        try
        {
            File.WriteAllText(
                configPath,
                $"{{\"trustedCertificates\":[\"sha256:{fingerprint}\"]}}");
            var configService = new AppConfigService();
            configService.Load();
            var store = new CertificateTrustStore(
                configService,
                _ => throw new InvalidOperationException("prompt should not be shown"));

            Assert.True(
                store.Validate(
                    "localhost",
                    certificate,
                    SslPolicyErrors.RemoteCertificateChainErrors));
        }
        finally
        {
            if (backup is null)
            {
                File.Delete(configPath);
            }
            else
            {
                File.WriteAllText(configPath, backup);
            }
        }
    }

    [Fact]
    public void ConfiguredCertificateFileIsTrustedWithoutPrompt()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));
        var certificatePath = Path.Combine(
            Path.GetTempPath(), $"fireflyfrp-{Guid.NewGuid():N}.crt");
        File.WriteAllText(certificatePath, certificate.ExportCertificatePem());

        var configPath = Path.Combine(AppPaths.BaseDirectory, "client.config.json");
        var backup = File.Exists(configPath) ? File.ReadAllText(configPath) : null;
        try
        {
            File.WriteAllText(
                configPath,
                "{\"trustedCertificates\":["
                + JsonSerializer.Serialize(certificatePath)
                + "]}");
            var configService = new AppConfigService();
            configService.Load();
            var store = new CertificateTrustStore(
                configService,
                _ => throw new InvalidOperationException("prompt should not be shown"));

            Assert.True(
                store.Validate(
                    "localhost",
                    certificate,
                    SslPolicyErrors.RemoteCertificateChainErrors));
        }
        finally
        {
            File.Delete(certificatePath);
            if (backup is null)
            {
                File.Delete(configPath);
            }
            else
            {
                File.WriteAllText(configPath, backup);
            }
        }
    }

    [Fact]
    public void NormalizeFingerprintAcceptsColonsAndPrefix()
    {
        var hex = new string('A', 64);
        var formatted = string.Join(":", Enumerable.Range(0, 32).Select(i => hex.Substring(i * 2, 2)));

        Assert.Equal(hex, CertificateTrustStore.NormalizeFingerprint($"sha256:{formatted}"));
        Assert.Equal(hex, CertificateTrustStore.NormalizeFingerprint(hex.ToLowerInvariant()));
        Assert.Null(CertificateTrustStore.NormalizeFingerprint("not-a-certificate"));
    }

    [Fact]
    public void ComputeSha256UsesRawCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        var fingerprint = CertificateTrustStore.ComputeSha256(certificate);

        Assert.Equal(64, fingerprint.Length);
        Assert.True(fingerprint.All(Uri.IsHexDigit));
    }
}
