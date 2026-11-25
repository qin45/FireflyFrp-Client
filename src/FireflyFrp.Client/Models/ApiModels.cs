using System.Text.Json.Serialization;

namespace FireflyFrp.Client.Models;

public sealed class LoginRequest
{
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("password")] public string Password { get; set; } = "";
}

public sealed class RegisterRequest
{
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("password")] public string Password { get; set; } = "";
}

public sealed class ChangePasswordRequest
{
    [JsonPropertyName("old_password")] public string OldPassword { get; set; } = "";
    [JsonPropertyName("new_password")] public string NewPassword { get; set; } = "";
}

public class UserSummary
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("traffic_quota_bytes")] public long TrafficQuotaBytes { get; set; }
    [JsonPropertyName("traffic_used_bytes")] public long TrafficUsedBytes { get; set; }
    [JsonPropertyName("traffic_remaining_bytes")] public long TrafficRemainingBytes { get; set; }
    [JsonPropertyName("rate_limit_mbps")] public int RateLimitMbps { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
}

public sealed class AuthResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("token_type")] public string TokenType { get; set; } = "bearer";
    [JsonPropertyName("expires_at")] public DateTime ExpiresAt { get; set; }
    [JsonPropertyName("user")] public UserSummary User { get; set; } = new();
}

public sealed class TunnelInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("local_ip")] public string LocalIp { get; set; } = "";
    [JsonPropertyName("local_port")] public int LocalPort { get; set; }
    [JsonPropertyName("remote_port")] public int? RemotePort { get; set; }
    [JsonPropertyName("subdomain")] public string? Subdomain { get; set; }
    [JsonPropertyName("domain")] public string? Domain { get; set; }
    [JsonPropertyName("public_address")] public string? PublicAddress { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("active")] public bool Active { get; set; }
    [JsonPropertyName("traffic_in_bytes")] public long TrafficInBytes { get; set; }
    [JsonPropertyName("traffic_out_bytes")] public long TrafficOutBytes { get; set; }
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }

    public string LocalTarget => $"{LocalIp}:{LocalPort}";
}

public sealed class UserInfo : UserSummary
{
    [JsonPropertyName("max_tunnels")] public int MaxTunnels { get; set; }
    [JsonPropertyName("max_active_tunnels")] public int MaxActiveTunnels { get; set; }
    [JsonPropertyName("enabled_tunnel_types")] public List<string> EnabledTunnelTypes { get; set; } = new();
    [JsonPropertyName("tunnels")] public List<TunnelInfo> Tunnels { get; set; } = new();
}

public sealed class CreateTunnelRequest
{
    [JsonPropertyName("type")] public string Type { get; set; } = "tcp";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("local_ip")] public string LocalIp { get; set; } = "127.0.0.1";
    [JsonPropertyName("local_port")] public int LocalPort { get; set; }
}

public sealed class TunnelCreateResponse
{
    [JsonPropertyName("tunnel")] public TunnelInfo Tunnel { get; set; } = new();
    [JsonPropertyName("token")] public string Token { get; set; } = "";
    [JsonPropertyName("frpc_config")] public string FrpcConfig { get; set; } = "";
}

public sealed class TunnelUpdateRequest
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("local_ip")] public string LocalIp { get; set; } = "127.0.0.1";
    [JsonPropertyName("local_port")] public int LocalPort { get; set; }
}

public sealed class TunnelUpdateResponse
{
    [JsonPropertyName("tunnel")] public TunnelInfo Tunnel { get; set; } = new();
    [JsonPropertyName("token")] public string? Token { get; set; }
    [JsonPropertyName("frpc_config")] public string? FrpcConfig { get; set; }
}

public sealed class TunnelTokenResponse
{
    [JsonPropertyName("token")] public string Token { get; set; } = "";
    [JsonPropertyName("frpc_config")] public string FrpcConfig { get; set; } = "";
}

public sealed class ApiErrorBody
{
    [JsonPropertyName("detail")] public object? Detail { get; set; }
}

public sealed class AuthSession
{
    public string ServerUrl { get; set; } = "";
    public string Username { get; set; } = "";
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public sealed class LoginResult
{
    public string ServerUrl { get; set; } = "";
    public string Username { get; set; } = "";
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public bool Remember { get; set; }
}
