using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace QZ.Frp.Client.Services;

public sealed record FrpcExitInfo(string TunnelId, int ExitCode, string LogTail);

public sealed class FrpcProcessManager : IDisposable
{
    private static readonly Regex AnsiEscape =
        new(@"\x1B\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled);

    private readonly ConcurrentDictionary<string, FrpcProcess> _processes = new();
    private readonly Func<string> _frpcPathProvider;

    public FrpcProcessManager(Func<string> frpcPathProvider)
    {
        _frpcPathProvider = frpcPathProvider;
    }

    public bool IsRunning(string tunnelId) => _processes.ContainsKey(tunnelId);

    public string GetLogTail(string tunnelId) =>
        _processes.TryGetValue(tunnelId, out var process) ? process.GetLogTail() : "";

    public Task StartAsync(
        string tunnelId,
        string frpcConfig,
        Action<FrpcExitInfo>? onExit = null,
        CancellationToken cancellationToken = default)
    {
        if (_processes.ContainsKey(tunnelId))
        {
            throw new InvalidOperationException("该隧道已经在本机运行");
        }

        var frpcPath = _frpcPathProvider();
        if (!File.Exists(frpcPath))
        {
            throw new FileNotFoundException("找不到 frpc.exe，请检查配置或内置资源。", frpcPath);
        }

        AppPaths.EnsureDirectories();
        var configPath = Path.Combine(AppPaths.RuntimeConfigDirectory, $"{tunnelId}.toml");
        var logPath = Path.Combine(AppPaths.LogsDirectory, $"frpc-{tunnelId}.log");
        var config = FrpcConfigComposer.EnsureLoginFailExit(frpcConfig);
        File.WriteAllText(configPath, config, new UTF8Encoding(false));

        var instance = new FrpcProcess(tunnelId, configPath, logPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = frpcPath,
            Arguments = $"-c \"{configPath}\"",
            WorkingDirectory = Path.GetDirectoryName(frpcPath) ?? AppPaths.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        instance.Process.StartInfo = startInfo;
        instance.Process.EnableRaisingEvents = true;
        instance.Process.OutputDataReceived += (_, e) => instance.Append(e.Data);
        instance.Process.ErrorDataReceived += (_, e) => instance.Append(e.Data);
        instance.Process.Exited += (_, _) => HandleExit(instance, onExit);

        // Register before Start so an immediate exit is not lost.
        _processes[tunnelId] = instance;
        try
        {
            if (!instance.Process.Start())
            {
                throw new InvalidOperationException("无法启动 frpc.exe");
            }
        }
        catch
        {
            _processes.TryRemove(tunnelId, out _);
            instance.DeleteConfig();
            instance.Dispose();
            throw;
        }

        instance.Process.BeginOutputReadLine();
        instance.Process.BeginErrorReadLine();
        return Task.CompletedTask;
    }

    public Task StopAsync(string tunnelId)
    {
        if (!_processes.TryRemove(tunnelId, out var instance))
        {
            return Task.CompletedTask;
        }
        instance.SuppressExitCallback = true;
        try
        {
            if (!instance.Process.HasExited)
            {
                instance.Process.Kill(entireProcessTree: true);
                instance.Process.WaitForExit(3000);
            }
        }
        catch
        {
            // Process may already be gone.
        }
        finally
        {
            instance.DeleteConfig();
            instance.Dispose();
        }
        return Task.CompletedTask;
    }

    public async Task StopAllAsync()
    {
        foreach (var tunnelId in _processes.Keys.ToList())
        {
            await StopAsync(tunnelId);
        }
    }

    private void HandleExit(FrpcProcess instance, Action<FrpcExitInfo>? onExit)
    {
        _processes.TryRemove(instance.TunnelId, out _);
        // Give output callbacks a brief chance to drain before reading the tail.
        Thread.Sleep(150);
        var info = new FrpcExitInfo(
            instance.TunnelId,
            SafeExitCode(instance.Process),
            instance.GetLogTail());
        instance.DeleteConfig();
        if (!instance.SuppressExitCallback)
        {
            try
            {
                onExit?.Invoke(info);
            }
            catch
            {
                // Never let a callback kill the process watcher.
            }
        }
        instance.Dispose();
    }

    private static int SafeExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    public void Dispose()
    {
        foreach (var instance in _processes.Values)
        {
            try
            {
                if (!instance.Process.HasExited)
                {
                    instance.Process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best effort.
            }
            instance.Dispose();
        }
        _processes.Clear();
    }

    private sealed class FrpcProcess : IDisposable
    {
        private readonly object _logLock = new();
        private readonly Queue<string> _logLines = new();
        private readonly StreamWriter? _logWriter;

        public string TunnelId { get; }
        public string ConfigPath { get; }
        public Process Process { get; } = new();
        public bool SuppressExitCallback { get; set; }

        public FrpcProcess(string tunnelId, string configPath, string logPath)
        {
            TunnelId = tunnelId;
            ConfigPath = configPath;
            try
            {
                _logWriter = new StreamWriter(logPath, append: false, Encoding.UTF8)
                {
                    AutoFlush = true,
                };
            }
            catch
            {
                _logWriter = null;
            }
        }

        public void Append(string? line)
        {
            if (line is null)
            {
                return;
            }
            line = AnsiEscape.Replace(line, "").TrimEnd();
            if (line.Length == 0)
            {
                return;
            }
            lock (_logLock)
            {
                _logLines.Enqueue(line);
                while (_logLines.Count > 300)
                {
                    _logLines.Dequeue();
                }
                try
                {
                    _logWriter?.WriteLine(line);
                }
                catch
                {
                    // Logging must not interfere with the tunnel.
                }
            }
        }

        public string GetLogTail(int count = 20)
        {
            lock (_logLock)
            {
                return string.Join(Environment.NewLine, _logLines.TakeLast(count));
            }
        }

        public void DeleteConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    File.Delete(ConfigPath);
                }
            }
            catch
            {
                // Best effort.
            }
        }

        public void Dispose()
        {
            try
            {
                _logWriter?.Dispose();
            }
            catch
            {
                // Ignore.
            }
            Process.Dispose();
        }
    }
}
