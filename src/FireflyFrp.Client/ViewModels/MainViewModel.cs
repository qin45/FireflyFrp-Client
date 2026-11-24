using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FireflyFrp.Client.Models;
using FireflyFrp.Client.Services;

namespace FireflyFrp.Client.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ClientSession _session;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private CancellationTokenSource? _pollCts;
    private bool _disposed;

    public event EventHandler? LogoutRequested;
    public event EventHandler? SessionExpired;
    public event EventHandler? TunnelsChanged;

    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _remainingText = "—";
    [ObservableProperty] private string _quotaText = "已用 — / 总配额 —";
    [ObservableProperty] private string _rateLimitText = "—";
    [ObservableProperty] private string _activeLimitText = "—";
    [ObservableProperty] private double _quotaPercent;
    [ObservableProperty] private bool _isConnected = true;
    [ObservableProperty] private string _connectionMessage = "";

    public ObservableCollection<TunnelCardViewModel> Tunnels { get; } = new();
    public ObservableCollection<string> EnabledTunnelTypes { get; } = new();

    public bool IsEmpty => Tunnels.Count == 0;
    public bool HasTunnels => Tunnels.Count > 0;
    public ClientSession Session => _session;

    public MainViewModel(ClientSession session)
    {
        _session = session;
        Username = session.Username;
        Tunnels.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasTunnels));
        };
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
        StartPolling();
    }

    public void StartPolling()
    {
        StopPolling();
        _pollCts = new CancellationTokenSource();
        _ = PollLoopAsync(_pollCts.Token);
    }

    public void StopPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        var interval = TimeSpan.FromSeconds(_session.Config.PollIntervalSeconds);
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, token);
                await RefreshAsync();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // RefreshAsync already reports connection errors.
            }
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_disposed)
        {
            return;
        }
        if (!await _refreshLock.WaitAsync(0))
        {
            return;
        }
        try
        {
            var user = await _session.Api.GetUserAsync();
            _session.CurrentUser = user;
            ApplyUser(user);
            IsConnected = true;
            ConnectionMessage = "";
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            StopPolling();
            SessionExpired?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            IsConnected = false;
            ConnectionMessage = "连接中断，正在重试…";
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private void ApplyUser(UserInfo user)
    {
        Username = user.Username;
        RemainingText = TunnelCardViewModel.FormatBytes(user.TrafficRemainingBytes);
        QuotaText =
            $"已用 {TunnelCardViewModel.FormatBytes(user.TrafficUsedBytes)} / 总配额 {TunnelCardViewModel.FormatBytes(user.TrafficQuotaBytes)}";
        RateLimitText = user.RateLimitMbps > 0 ? $"{user.RateLimitMbps} Mbps" : "不限速";
        ActiveLimitText = $"{user.MaxActiveTunnels} 条";
        QuotaPercent = user.TrafficQuotaBytes > 0
            ? Math.Clamp(user.TrafficUsedBytes * 100.0 / user.TrafficQuotaBytes, 0, 100)
            : 0;

        EnabledTunnelTypes.Clear();
        foreach (var type in user.EnabledTunnelTypes)
        {
            EnabledTunnelTypes.Add(type);
        }

        var existing = Tunnels.ToDictionary(t => t.Id);
        var seen = new HashSet<string>();
        foreach (var info in user.Tunnels)
        {
            seen.Add(info.Id);
            if (existing.TryGetValue(info.Id, out var card))
            {
                card.UpdateFrom(info);
            }
            else
            {
                var created = new TunnelCardViewModel(_session, info, RefreshAsync);
                created.Deleted += OnCardDeleted;
                Tunnels.Add(created);
            }
        }

        foreach (var card in Tunnels.Where(t => !seen.Contains(t.Id)).ToList())
        {
            Tunnels.Remove(card);
        }
        TunnelsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnCardDeleted(object? sender, EventArgs e)
    {
        if (sender is TunnelCardViewModel card)
        {
            Tunnels.Remove(card);
            _ = RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task StopAllAsync()
    {
        await StopAllTunnelsAsync();
    }

    public async Task StopAllTunnelsAsync()
    {
        foreach (var card in Tunnels.ToList())
        {
            await card.StopForShutdownAsync();
        }
        await RefreshAsync();
    }

    [RelayCommand]
    private void Logout()
    {
        StopPolling();
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    public Task ReloadAsync() => _disposed ? Task.CompletedTask : RefreshAsync();

    public async Task StopAllForShutdownAsync()
    {
        StopPolling();
        foreach (var card in Tunnels.ToList())
        {
            await card.StopForShutdownAsync();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        StopPolling();
    }
}
