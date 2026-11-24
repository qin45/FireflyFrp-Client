using System.Net;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FireflyFrp.Client.Models;
using FireflyFrp.Client.Services;

namespace FireflyFrp.Client.ViewModels;

public sealed partial class TunnelCardViewModel : ObservableObject
{
    private static readonly Brush OnlineBrush = new SolidColorBrush(Color.FromRgb(63, 185, 80));
    private static readonly Brush StartingBrush = new SolidColorBrush(Color.FromRgb(91, 140, 255));
    private static readonly Brush StoppingBrush = new SolidColorBrush(Color.FromRgb(210, 153, 34));
    private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(229, 83, 75));
    private static readonly Brush RemoteBrush = new SolidColorBrush(Color.FromRgb(139, 148, 158));

    private readonly ClientSession _session;
    private readonly Func<Task> _refresh;
    private TunnelInfo _model;
    private bool _stopRequested;

    public event EventHandler? Deleted;

    [ObservableProperty] private string _id;
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _typeLabel;
    [ObservableProperty] private string _localTarget;
    [ObservableProperty] private string _publicAddress = "";
    [ObservableProperty] private string _statusText = "隧道已关闭";
    [ObservableProperty] private string _errorText = "";
    [ObservableProperty] private string _trafficText = "流量 0 B";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _canCopy;
    [ObservableProperty] private bool _isTakeoverVisible;
    [ObservableProperty] private string _copyButtonText = "复制";
    [ObservableProperty] private Brush _statusBrush = OnlineBrush;

    public bool CanToggle => !IsBusy;
    public string ServerUrl => _session.ServerUrl;

    public TunnelCardViewModel(ClientSession session, TunnelInfo model, Func<Task> refresh)
    {
        _session = session;
        _model = model;
        _refresh = refresh;
        _id = model.Id;
        _name = string.IsNullOrWhiteSpace(model.Name) ? model.Id[..8] : model.Name;
        _typeLabel = model.Type.ToUpperInvariant();
        _localTarget = model.LocalTarget;
        UpdateFrom(model);
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanToggle));
        if (!value)
        {
            IsTakeoverVisible = IsActive && !IsRunning;
        }
    }

    public void UpdateFrom(TunnelInfo info)
    {
        _model = info;
        Name = string.IsNullOrWhiteSpace(info.Name) ? info.Id[..8] : info.Name;
        TypeLabel = info.Type.ToUpperInvariant();
        LocalTarget = info.LocalTarget;
        PublicAddress = info.PublicAddress ?? "";
        IsActive = info.Active;
        TrafficText = $"流量 {FormatBytes(info.TrafficInBytes + info.TrafficOutBytes)}";
        CanCopy = IsActive && !string.IsNullOrEmpty(PublicAddress);
        IsTakeoverVisible = IsActive && !IsRunning && !IsBusy;

        if (IsRunning && IsActive)
        {
            StatusText = "在线";
            StatusBrush = OnlineBrush;
        }
        else if (IsRunning)
        {
            StatusText = "连接中";
            StatusBrush = StartingBrush;
        }
        else if (IsActive)
        {
            StatusText = "其他客户端";
            StatusBrush = RemoteBrush;
        }
        else if (string.IsNullOrWhiteSpace(ErrorText))
        {
            StatusText = "隧道已关闭";
            StatusBrush = OnlineBrush;
        }
    }

    [RelayCommand]
    private async Task ToggleAsync()
    {
        if (IsBusy)
        {
            return;
        }
        if (IsRunning)
        {
            await StopAsync();
        }
        else
        {
            await StartAsync();
        }
    }

    private async Task StartAsync(bool forceRotate = false)
    {
        _stopRequested = false;
        IsBusy = true;
        ErrorText = "";
        CopyButtonText = "复制";
        try
        {
            UserInfo? user = _session.CurrentUser;
            if (user is null)
            {
                user = await _session.Api.GetUserAsync();
                _session.CurrentUser = user;
            }
            if (user.TrafficRemainingBytes <= 0)
            {
                ErrorText = "流量不足，请联系管理员充值。";
                StatusText = "无法启动";
                StatusBrush = ErrorBrush;
                return;
            }
            if (!user.EnabledTunnelTypes.Contains(_model.Type))
            {
                ErrorText = $"该节点已禁用 {TypeLabel} 隧道。";
                StatusText = "无法启动";
                StatusBrush = ErrorBrush;
                return;
            }

            var storedConfig = forceRotate ? null : _session.TunnelConfigs.Load(Id);
            var needsRotate = forceRotate || IsActive || string.IsNullOrWhiteSpace(storedConfig);
            if (needsRotate)
            {
                var message = IsActive
                    ? "该隧道正在其他客户端运行。继续将重置令牌并断开其他客户端，是否继续？"
                    : "本机没有该隧道的启动配置，将重置令牌并获取新配置，旧令牌会立即失效。是否继续？";
                if (MessageBox.Show(
                        message,
                        "启动隧道",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            var config = storedConfig;
            if (string.IsNullOrWhiteSpace(config))
            {
                config = await RotateAndSaveAsync();
            }

            StatusText = "启动中…";
            StatusBrush = StartingBrush;
            await _session.Frpc.StartAsync(Id, config, OnFrpcExit);
            IsRunning = true;
            IsTakeoverVisible = false;

            var active = await WaitForActiveAsync();
            if (!active)
            {
                if (_session.Frpc.IsRunning(Id))
                {
                    await _session.Frpc.StopAsync(Id);
                }
                IsRunning = false;
                if (string.IsNullOrWhiteSpace(ErrorText))
                {
                    ErrorText = "隧道未上线，请查看 frpc 日志。";
                }
                StatusText = "启动失败";
                StatusBrush = ErrorBrush;
                return;
            }

            StatusText = "在线";
            StatusBrush = OnlineBrush;
            await SafeRefreshAsync();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            ErrorText = "登录已失效，请重新登录。";
        }
        catch (Exception ex)
        {
            IsRunning = _session.Frpc.IsRunning(Id);
            ErrorText = ex.Message;
            StatusText = IsRunning ? "连接异常" : "启动失败";
            StatusBrush = ErrorBrush;
        }
        finally
        {
            IsBusy = false;
            IsTakeoverVisible = IsActive && !IsRunning;
            // If a start failed, re-apply the switch position from the source.
            OnPropertyChanged(nameof(IsRunning));
        }
    }

    private async Task StopAsync()
    {
        _stopRequested = true;
        IsBusy = true;
        ErrorText = "";
        try
        {
            StatusText = "停止中…";
            StatusBrush = StoppingBrush;
            await _session.Frpc.StopAsync(Id);
            IsRunning = false;
            IsTakeoverVisible = IsActive;
            await SafeRefreshAsync();
            if (IsActive)
            {
                StatusText = "其他客户端";
                StatusBrush = RemoteBrush;
            }
            else
            {
                StatusText = "隧道已关闭";
                StatusBrush = OnlineBrush;
            }
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            StatusText = "停止失败";
            StatusBrush = ErrorBrush;
        }
        finally
        {
            IsBusy = false;
            IsTakeoverVisible = IsActive && !IsRunning;
            OnPropertyChanged(nameof(IsRunning));
        }
    }

    [RelayCommand]
    private async Task TakeoverAsync()
    {
        if (IsBusy)
        {
            return;
        }
        await StartAsync(forceRotate: true);
    }

    [RelayCommand]
    private async Task CopyAddressAsync()
    {
        if (!CanCopy)
        {
            return;
        }
        try
        {
            Clipboard.SetText(PublicAddress);
            CopyButtonText = "已复制";
            await Task.Delay(1800);
            CopyButtonText = "复制";
        }
        catch
        {
            ErrorText = "复制失败，请重试。";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsBusy)
        {
            return;
        }
        if (MessageBox.Show(
                IsActive
                    ? "删除后会先断开该隧道，其他隧道不受影响。确定删除？"
                    : "确定删除该隧道？",
                "删除隧道",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        _stopRequested = true;
        IsBusy = true;
        ErrorText = "";
        try
        {
            await _session.Frpc.StopAsync(Id);
            await _session.Api.DeleteTunnelAsync(Id);
            _session.TunnelConfigs.Delete(Id);
            Deleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task StopForShutdownAsync()
    {
        _stopRequested = true;
        try
        {
            await _session.Frpc.StopAsync(Id);
        }
        catch
        {
            // Best effort during shutdown.
        }
        IsRunning = false;
    }

    private async Task<string> RotateAndSaveAsync()
    {
        var response = await _session.Api.RotateTunnelTokenAsync(Id);
        if (string.IsNullOrWhiteSpace(response.FrpcConfig))
        {
            throw new InvalidOperationException("服务端没有返回 frpc 配置");
        }
        _session.TunnelConfigs.Save(Id, response.FrpcConfig);
        return response.FrpcConfig;
    }

    private async Task<bool> WaitForActiveAsync()
    {
        for (var i = 0; i < 20; i++)
        {
            if (!_session.Frpc.IsRunning(Id))
            {
                return false;
            }
            try
            {
                var user = await _session.Api.GetUserAsync();
                _session.CurrentUser = user;
                var tunnel = user.Tunnels.FirstOrDefault(t => t.Id == Id);
                if (tunnel is { Active: true })
                {
                    UpdateFrom(tunnel);
                    return true;
                }
            }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                return false;
            }
            catch
            {
                // Transient network error: keep waiting until the deadline.
            }
            await Task.Delay(500);
        }
        return false;
    }

    private void OnFrpcExit(FrpcExitInfo info)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            IsRunning = false;
            IsBusy = false;
            IsTakeoverVisible = IsActive;
            if (_stopRequested)
            {
                return;
            }
            if (!string.IsNullOrWhiteSpace(ErrorText))
            {
                return;
            }

            var tail = info.LogTail
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault();
            var looksLikeError = info.ExitCode != 0 || ContainsError(tail);
            if (!looksLikeError)
            {
                StatusText = "隧道已关闭";
                StatusBrush = OnlineBrush;
                _ = SafeRefreshAsync();
                return;
            }

            ErrorText = string.IsNullOrWhiteSpace(tail)
                ? $"frpc 异常退出（代码 {info.ExitCode}）"
                : $"frpc 异常退出：{tail}";
            StatusText = "连接断开";
            StatusBrush = ErrorBrush;
            _ = SafeRefreshAsync();
        });
    }

    private static bool ContainsError(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        return text.Contains("error", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("fail", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("panic", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("fatal", StringComparison.OrdinalIgnoreCase);
    }

    private async Task SafeRefreshAsync()
    {
        try
        {
            await _refresh();
        }
        catch
        {
            // Polling/refresh failures are surfaced by the main view model.
        }
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} B" : $"{value:0.00} {units[unit]}";
    }
}
