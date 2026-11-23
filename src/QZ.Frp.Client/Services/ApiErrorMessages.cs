using System.Net;
using System.Text.RegularExpressions;

namespace QZ.Frp.Client.Services;

/// <summary>
/// Translates the API's English error details into user-facing Chinese.
/// Unknown English messages fall back to a generic message for the status code.
/// </summary>
public static class ApiErrorMessages
{
    private static readonly Dictionary<string, string> Exact = new(StringComparer.OrdinalIgnoreCase)
    {
        ["invalid username or password"] = "用户名或密码错误",
        ["user is disabled"] = "账号已被禁用，请联系管理员",
        ["username already exists"] = "用户名已存在",
        ["old password is incorrect"] = "原密码不正确",
        ["invalid session"] = "登录已失效，请重新登录",
        ["session expired"] = "登录已失效，请重新登录",
        ["missing bearer token"] = "登录已失效，请重新登录",
        ["tunnel not found"] = "隧道不存在",
        ["user not found"] = "用户不存在",
        ["not found"] = "请求的资源不存在",
        ["http tunnels are disabled on this node"] = "当前节点已禁用 HTTP 隧道",
        ["https tunnels are disabled on this node"] = "当前节点已禁用 HTTPS 隧道",
        ["invalid tunnel type"] = "无效的隧道类型",
        ["no free remote port in configured range"] = "没有可用的远程端口，请稍后重试",
        ["source ip is not trusted"] = "来源地址不受信任",
        ["invalid json"] = "请求格式错误",
        ["invalid admin token"] = "管理员令牌无效",
        ["server url is not configured"] = "服务端地址未配置",
        ["access token is not configured"] = "登录状态未配置，请重新登录",
    };

    private static readonly Regex AtMostTunnels =
        new(@"^at most (\d+) tunnels per user$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AtLeastCharacters =
        new(@"String should have at least (\d+) characters", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AtMostCharacters =
        new(@"String should have at most (\d+) characters", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Localize(string? message, HttpStatusCode statusCode)
    {
        var text = message?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return Fallback(statusCode);
        }
        if (ContainsChinese(text))
        {
            return text;
        }
        if (Exact.TryGetValue(text, out var exact))
        {
            return exact;
        }

        var match = AtMostTunnels.Match(text);
        if (match.Success)
        {
            return $"每个用户最多创建 {match.Groups[1].Value} 条隧道";
        }
        match = AtLeastCharacters.Match(text);
        if (match.Success)
        {
            return $"长度不能少于 {match.Groups[1].Value} 个字符";
        }
        match = AtMostCharacters.Match(text);
        if (match.Success)
        {
            return $"长度不能超过 {match.Groups[1].Value} 个字符";
        }
        if (text.StartsWith("could not allocate tunnel resource", StringComparison.OrdinalIgnoreCase))
        {
            return "无法分配隧道资源，请稍后重试";
        }
        if (text.StartsWith("frps admin API unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return "frps 管理接口不可用，请稍后重试";
        }
        if (text.Contains("String should match pattern", StringComparison.OrdinalIgnoreCase))
        {
            return "用户名格式不正确，需为 3-32 位字母、数字、下划线或短横线";
        }
        if (text.Contains("Input should be a valid integer", StringComparison.OrdinalIgnoreCase))
        {
            return "请输入有效的整数";
        }
        if (text.Contains("Field required", StringComparison.OrdinalIgnoreCase))
        {
            return "请填写必填项";
        }
        if (text.Contains("Input should be 'tcp'", StringComparison.OrdinalIgnoreCase))
        {
            return "无效的隧道类型";
        }
        return Fallback(statusCode);
    }

    public static string Fallback(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => "请求参数有误，请检查输入",
        HttpStatusCode.Unauthorized => "登录已失效，请重新登录",
        HttpStatusCode.Forbidden => "没有权限执行此操作",
        HttpStatusCode.NotFound => "请求的资源不存在",
        HttpStatusCode.Conflict => "操作冲突，请检查当前状态",
        HttpStatusCode.UnprocessableEntity => "请求参数有误，请检查输入",
        (HttpStatusCode)429 => "请求过于频繁，请稍后重试",
        HttpStatusCode.InternalServerError => "服务器内部错误，请稍后重试或联系管理员",
        HttpStatusCode.BadGateway => "网关错误，请稍后重试",
        HttpStatusCode.ServiceUnavailable => "服务暂时不可用，请稍后重试",
        _ => $"请求失败（{(int)statusCode}）",
    };

    private static bool ContainsChinese(string text) =>
        text.Any(ch => ch is >= '一' and <= '鿿');
}
