using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace FireflyFrp.Client.Services;

public sealed record CertificateTrustRequest(
    string Host,
    string Subject,
    string Issuer,
    string Sha256,
    DateTime NotBefore,
    DateTime NotAfter,
    SslPolicyErrors Errors);

/// <summary>
/// Validates TLS certificates and remembers certificates explicitly trusted by
/// the user. Certificates can also be pre-trusted in client.config.json.
/// </summary>
public sealed class CertificateTrustStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly AppConfigService _configService;
    private readonly Func<CertificateTrustRequest, bool> _prompt;
    private readonly HashSet<string> _trusted = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TrustedCertificateEntry> _entries = new();
    private readonly object _lock = new();
    private readonly object _promptLock = new();

    public CertificateTrustStore(
        AppConfigService configService,
        Func<CertificateTrustRequest, bool> prompt)
    {
        _configService = configService;
        _prompt = prompt;
        LoadConfiguredCertificates();
        LoadPersistedCertificates();
    }

    public bool Validate(string host, X509Certificate2? certificate, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None)
        {
            return true;
        }
        if (certificate is null)
        {
            return false;
        }

        var sha256 = ComputeSha256(certificate);
        lock (_lock)
        {
            if (_trusted.Contains(sha256))
            {
                return true;
            }
        }

        var request = new CertificateTrustRequest(
            host,
            certificate.Subject,
            certificate.Issuer,
            sha256,
            certificate.NotBefore.ToLocalTime(),
            certificate.NotAfter.ToLocalTime(),
            errors);

        bool trust;
        lock (_promptLock)
        {
            lock (_lock)
            {
                if (_trusted.Contains(sha256))
                {
                    return true;
                }
            }
            try
            {
                trust = _prompt(request);
            }
            catch
            {
                trust = false;
            }
        }

        if (!trust)
        {
            return false;
        }

        lock (_lock)
        {
            _trusted.Add(sha256);
        }
        PersistCertificate(request);
        return true;
    }

    public static string ComputeSha256(X509Certificate2 certificate) =>
        Convert.ToHexString(SHA256.HashData(certificate.RawData));

    public static string? NormalizeFingerprint(string value)
    {
        var compact = value
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim();
        if (compact.StartsWith("sha256", StringComparison.OrdinalIgnoreCase))
        {
            compact = compact[6..];
        }
        return compact.Length == 64 && compact.All(Uri.IsHexDigit)
            ? compact.ToUpperInvariant()
            : null;
    }

    private void LoadConfiguredCertificates()
    {
        foreach (var value in _configService.Config.TrustedCertificates)
        {
            var fingerprint = NormalizeFingerprint(value);
            if (fingerprint is not null)
            {
                _trusted.Add(fingerprint);
                continue;
            }

            var path = Path.IsPathRooted(value)
                ? value
                : Path.Combine(AppPaths.BaseDirectory, value);
            if (!File.Exists(path))
            {
                continue;
            }
            try
            {
                using var certificate = LoadCertificate(path);
                _trusted.Add(ComputeSha256(certificate));
            }
            catch
            {
                // A bad optional certificate must not prevent the client from starting.
            }
        }
    }

    private void LoadPersistedCertificates()
    {
        try
        {
            if (!File.Exists(AppPaths.TrustedCertificatesFile))
            {
                return;
            }
            var json = File.ReadAllText(AppPaths.TrustedCertificatesFile);
            var entries = JsonSerializer.Deserialize<List<TrustedCertificateEntry>>(
                json, JsonOptions);
            if (entries is null)
            {
                return;
            }
            foreach (var entry in entries)
            {
                var fingerprint = NormalizeFingerprint(entry.Sha256);
                if (fingerprint is null)
                {
                    continue;
                }
                _trusted.Add(fingerprint);
                _entries.Add(entry);
            }
        }
        catch
        {
            // Corrupt trust data is ignored; certificates can be trusted again.
        }
    }

    private void PersistCertificate(CertificateTrustRequest request)
    {
        lock (_lock)
        {
            if (_entries.Any(entry =>
                    string.Equals(
                        NormalizeFingerprint(entry.Sha256),
                        request.Sha256,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            _entries.Add(new TrustedCertificateEntry
            {
                Host = request.Host,
                Subject = request.Subject,
                Issuer = request.Issuer,
                Sha256 = request.Sha256,
                NotAfter = request.NotAfter,
                TrustedAt = DateTime.UtcNow,
            });
            try
            {
                AppPaths.EnsureDirectories();
                var json = JsonSerializer.Serialize(_entries, JsonOptions);
                var temp = AppPaths.TrustedCertificatesFile + ".tmp";
                File.WriteAllText(temp, json);
                File.Move(temp, AppPaths.TrustedCertificatesFile, overwrite: true);
            }
            catch
            {
                // Trust remains valid for this process even if persistence fails.
            }
        }
    }

    private static X509Certificate2 LoadCertificate(string path)
    {
        try
        {
            return X509CertificateLoader.LoadCertificateFromFile(path);
        }
        catch
        {
            return X509Certificate2.CreateFromPemFile(path);
        }
    }

    private sealed class TrustedCertificateEntry
    {
        public string Host { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Issuer { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public DateTime NotAfter { get; set; }
        public DateTime TrustedAt { get; set; }
    }
}
