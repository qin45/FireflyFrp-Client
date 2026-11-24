namespace FireflyFrp.Client.Services;

public static class FrpcConfigComposer
{
    /// <summary>
    /// Make frpc exit on a failed login instead of retrying forever, so the UI
    /// can surface quota/active-limit/token errors immediately.
    /// </summary>
    public static string EnsureLoginFailExit(string config)
    {
        if (config.Contains("loginFailExit", StringComparison.Ordinal))
        {
            return config;
        }

        var markerIndex = config.IndexOf("[[proxies]]", StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return "loginFailExit = true\n" + config;
        }

        return config.Insert(markerIndex, "loginFailExit = true\n\n");
    }
}
