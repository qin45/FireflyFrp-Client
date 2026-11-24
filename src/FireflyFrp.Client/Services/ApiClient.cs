using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FireflyFrp.Client.Models;

namespace FireflyFrp.Client.Services;

public sealed class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public ApiException(HttpStatusCode statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}

public sealed class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private string? _serverUrl;
    private string? _token;

    public ApiClient(HttpClient? http = null, TimeSpan? timeout = null)
    {
        _http = http ?? new HttpClient();
        _http.Timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    public bool HasSession => !string.IsNullOrWhiteSpace(_serverUrl) && !string.IsNullOrWhiteSpace(_token);

    public void SetSession(string serverUrl, string token)
    {
        _serverUrl = serverUrl.Trim().TrimEnd('/');
        _token = token;
    }

    public void ClearSession()
    {
        _serverUrl = null;
        _token = null;
    }

    public async Task<AuthResponse> LoginAsync(
        string serverUrl,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var body = new LoginRequest { Username = username, Password = password };
        return await SendAsync<AuthResponse>(
            HttpMethod.Post,
            serverUrl,
            "/api/v1/auth/login",
            body,
            requireAuth: false,
            cancellationToken);
    }

    public async Task<UserSummary> RegisterAsync(
        string serverUrl,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var body = new RegisterRequest { Username = username, Password = password };
        return await SendAsync<UserSummary>(
            HttpMethod.Post,
            serverUrl,
            "/api/v1/auth/register",
            body,
            requireAuth: false,
            cancellationToken);
    }

    public async Task ChangePasswordAsync(
        string oldPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var body = new ChangePasswordRequest
        {
            OldPassword = oldPassword,
            NewPassword = newPassword,
        };
        await SendAsync<object>(
            HttpMethod.Post,
            null,
            "/api/v1/auth/password",
            body,
            true,
            cancellationToken);
    }

    public Task<UserInfo> GetUserAsync(CancellationToken cancellationToken = default) =>
        SendAsync<UserInfo>(HttpMethod.Get, null, "/api/v1/user", null, true, cancellationToken);

    public Task<TunnelCreateResponse> CreateTunnelAsync(
        CreateTunnelRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<TunnelCreateResponse>(
            HttpMethod.Post, null, "/api/v1/tunnels", request, true, cancellationToken);

    public async Task DeleteTunnelAsync(string tunnelId, CancellationToken cancellationToken = default)
    {
        await SendAsync<object>(
            HttpMethod.Delete,
            null,
            $"/api/v1/tunnels/{Uri.EscapeDataString(tunnelId)}",
            null,
            true,
            cancellationToken);
    }

    public Task<TunnelTokenResponse> RotateTunnelTokenAsync(
        string tunnelId,
        CancellationToken cancellationToken = default) =>
        SendAsync<TunnelTokenResponse>(
            HttpMethod.Post,
            null,
            $"/api/v1/tunnels/{Uri.EscapeDataString(tunnelId)}/token",
            null,
            true,
            cancellationToken);

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SendAsync<object>(
                HttpMethod.Post, null, "/api/v1/auth/logout", null, true, cancellationToken);
        }
        catch
        {
            // Logout is best-effort; local state is cleared regardless.
        }
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string? explicitServerUrl,
        string path,
        object? body,
        bool requireAuth,
        CancellationToken cancellationToken)
    {
        var serverUrl = explicitServerUrl?.Trim().TrimEnd('/') ?? _serverUrl;
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            throw new InvalidOperationException("服务端地址未配置");
        }
        if (requireAuth && string.IsNullOrWhiteSpace(_token))
        {
            throw new InvalidOperationException("登录状态未配置，请重新登录");
        }

        var uri = new Uri($"{serverUrl}/", UriKind.Absolute);
        var request = new HttpRequestMessage(method, new Uri(uri, path.TrimStart('/')));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (requireAuth)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }
        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await _http.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(
                response.StatusCode,
                ParseError(content, response.StatusCode));
        }
        if (string.IsNullOrWhiteSpace(content) || typeof(T) == typeof(object))
        {
            return default!;
        }
        var value = JsonSerializer.Deserialize<T>(content, JsonOptions);
        if (value is null)
        {
            throw new ApiException(response.StatusCode, "服务器返回了空响应");
        }
        return value;
    }

    private static string ParseError(string content, HttpStatusCode statusCode)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("detail", out var detail))
                {
                    if (detail.ValueKind == JsonValueKind.String)
                    {
                        return ApiErrorMessages.Localize(detail.GetString(), statusCode);
                    }
                    if (detail.ValueKind == JsonValueKind.Array)
                    {
                        var messages = new List<string>();
                        foreach (var item in detail.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object &&
                                item.TryGetProperty("msg", out var msg) &&
                                msg.ValueKind == JsonValueKind.String)
                            {
                                messages.Add(ApiErrorMessages.Localize(msg.GetString(), statusCode));
                            }
                            else if (item.ValueKind == JsonValueKind.String)
                            {
                                messages.Add(ApiErrorMessages.Localize(item.GetString(), statusCode));
                            }
                        }
                        var distinct = messages
                            .Where(m => !string.IsNullOrWhiteSpace(m))
                            .Distinct()
                            .ToList();
                        if (distinct.Count > 0)
                        {
                            return string.Join("；", distinct);
                        }
                    }
                }
            }
            catch
            {
                // Fall through to a generic message.
            }
        }
        return ApiErrorMessages.Fallback(statusCode);
    }
}
