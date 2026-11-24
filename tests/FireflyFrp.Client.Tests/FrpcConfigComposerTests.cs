using FireflyFrp.Client.Services;

namespace FireflyFrp.Client.Tests;

public sealed class FrpcConfigComposerTests
{
    [Fact]
    public void InsertsLoginFailExitBeforeProxies()
    {
        const string config = "serverAddr = \"example.com\"\n\n[[proxies]]\nname = \"t\"\n";
        var result = FrpcConfigComposer.EnsureLoginFailExit(config);
        Assert.Contains("loginFailExit = true", result);
        Assert.True(result.IndexOf("loginFailExit", StringComparison.Ordinal) <
                    result.IndexOf("[[proxies]]", StringComparison.Ordinal));
    }

    [Fact]
    public void DoesNotDuplicateExistingSetting()
    {
        const string config = "loginFailExit = true\n[[proxies]]\nname = \"t\"\n";
        var result = FrpcConfigComposer.EnsureLoginFailExit(config);
        Assert.Equal(1, result.Split("loginFailExit", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void PrependsWhenNoProxiesSection()
    {
        var result = FrpcConfigComposer.EnsureLoginFailExit("serverAddr = \"example.com\"\n");
        Assert.StartsWith("loginFailExit = true", result);
    }
}
