using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FireflyFrp.Client.Models;
using FireflyFrp.Client.Services;

namespace FireflyFrp.Client.Tests;

public sealed class ApiClientTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responder(request));
        }
    }

    [Fact]
    public async Task LoginParsesResponseAndSendsJson()
    {
        string? body = null;
        var handler = new RecordingHandler(request =>
        {
            body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            var json = JsonSerializer.Serialize(new AuthResponse
            {
                AccessToken = "token-1",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                User = new UserSummary { Username = "alice", RateLimitMbps = 10 },
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        });

        var api = new ApiClient(new HttpClient(handler));
        var result = await api.LoginAsync("http://localhost:8080", "alice", "password123");

        Assert.Equal("token-1", result.AccessToken);
        Assert.Contains("\"username\":\"alice\"", body);
        Assert.Equal("http://localhost:8080/api/v1/auth/login",
            handler.Requests[0].RequestUri?.ToString());
    }

    [Fact]
    public async Task RegisterSendsRequestAndParsesUser()
    {
        string? body = null;
        var handler = new RecordingHandler(request =>
        {
            body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    "{\"id\":1,\"username\":\"newuser\",\"traffic_quota_bytes\":0,\"traffic_used_bytes\":0,\"traffic_remaining_bytes\":0,\"rate_limit_mbps\":10,\"status\":\"active\",\"created_at\":\"2026-01-01T00:00:00\"}",
                    Encoding.UTF8,
                    "application/json"),
            };
        });
        var api = new ApiClient(new HttpClient(handler));

        var user = await api.RegisterAsync("http://localhost:8080", "newuser", "password123");

        Assert.Equal("newuser", user.Username);
        Assert.Contains("\"username\":\"newuser\"", body);
        Assert.Equal("http://localhost:8080/api/v1/auth/register",
            handler.Requests[0].RequestUri?.ToString());
    }

    [Fact]
    public async Task GetUserSendsBearerToken()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"id\":1,\"username\":\"alice\",\"traffic_quota_bytes\":10,\"traffic_used_bytes\":2,\"traffic_remaining_bytes\":8,\"rate_limit_mbps\":10,\"status\":\"active\",\"created_at\":\"2026-01-01T00:00:00\",\"max_tunnels\":10,\"max_active_tunnels\":3,\"enabled_tunnel_types\":[\"tcp\",\"udp\"],\"tunnels\":[]}",
                    Encoding.UTF8,
                    "application/json"),
            });
        var api = new ApiClient(new HttpClient(handler));
        api.SetSession("http://localhost:8080/", "token-1");

        var user = await api.GetUserAsync();

        Assert.Equal("alice", user.Username);
        Assert.Equal(8, user.TrafficRemainingBytes);
        Assert.Equal("Bearer", handler.Requests[0].Headers.Authorization?.Scheme);
        Assert.Equal("token-1", handler.Requests[0].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task ErrorDetailBecomesApiException()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    "{\"detail\":\"HTTP tunnels are disabled on this node\"}",
                    Encoding.UTF8,
                    "application/json"),
            });
        var api = new ApiClient(new HttpClient(handler));
        api.SetSession("http://localhost:8080", "token-1");

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => api.CreateTunnelAsync(new CreateTunnelRequest
            {
                Type = "http",
                Name = "web",
                LocalPort = 8080,
            }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("当前节点已禁用 HTTP 隧道", ex.Message);
    }

    [Fact]
    public async Task LoginErrorIsLocalized()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(
                    "{\"detail\":\"invalid username or password\"}",
                    Encoding.UTF8,
                    "application/json"),
            });
        var api = new ApiClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => api.LoginAsync("http://localhost:8080", "alice", "wrong-password"));

        Assert.Equal("用户名或密码错误", ex.Message);
    }

    [Fact]
    public async Task ChangePasswordUsesTheExpectedEndpoint()
    {
        string? body = null;
        var handler = new RecordingHandler(request =>
        {
            body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.NoContent)
            {
                Content = new StringContent(string.Empty),
            };
        });
        var api = new ApiClient(new HttpClient(handler));
        api.SetSession("http://localhost:8080", "token-1");

        await api.ChangePasswordAsync("old-password", "new-password");

        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(
            "http://localhost:8080/api/v1/auth/password",
            handler.Requests[0].RequestUri?.ToString());
        Assert.Contains("\"old_password\":\"old-password\"", body);
        Assert.Contains("\"new_password\":\"new-password\"", body);
    }

    [Fact]
    public async Task UpdateTunnelSendsPatchWithEndpoints()
    {
        string? body = null;
        var handler = new RecordingHandler(request =>
        {
            body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"tunnel\":{\"id\":\"abc\",\"name\":\"renamed\",\"type\":\"tcp\",\"local_ip\":\"10.0.0.5\",\"local_port\":2222,\"remote_port\":20000,\"subdomain\":null,\"domain\":null,\"public_address\":\"frp.test:20000\",\"status\":\"inactive\",\"active\":false,\"traffic_in_bytes\":0,\"traffic_out_bytes\":0,\"created_at\":\"2026-01-01T00:00:00\"},\"token\":null,\"frpc_config\":null}",
                    Encoding.UTF8,
                    "application/json"),
            };
        });
        var api = new ApiClient(new HttpClient(handler));
        api.SetSession("http://localhost:8080", "token-1");

        var result = await api.UpdateTunnelAsync(
            "abc",
            new TunnelUpdateRequest
            {
                Name = "renamed",
                LocalIp = "10.0.0.5",
                LocalPort = 2222,
            });

        Assert.Equal(HttpMethod.Patch, handler.Requests[0].Method);
        Assert.Equal(
            "http://localhost:8080/api/v1/tunnels/abc",
            handler.Requests[0].RequestUri?.ToString());
        Assert.Contains("\"name\":\"renamed\"", body);
        Assert.Contains("\"local_ip\":\"10.0.0.5\"", body);
        Assert.Contains("\"local_port\":2222", body);
        Assert.Equal("renamed", result.Tunnel.Name);
        Assert.Null(result.FrpcConfig);
    }
}
