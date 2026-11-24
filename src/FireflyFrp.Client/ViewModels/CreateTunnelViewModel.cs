using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FireflyFrp.Client.Models;
using FireflyFrp.Client.Services;

namespace FireflyFrp.Client.ViewModels;

public sealed partial class CreateTunnelViewModel : ObservableObject
{
    private readonly ClientSession _session;

    public event EventHandler<TunnelCreateResponse>? Created;

    [ObservableProperty] private string _selectedType = "tcp";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _localIp = "127.0.0.1";
    [ObservableProperty] private string _localPortText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _errorMessage = "";

    public ObservableCollection<string> Types { get; } = new();

    public CreateTunnelViewModel(ClientSession session, IEnumerable<string> enabledTypes)
    {
        _session = session;
        foreach (var type in enabledTypes)
        {
            Types.Add(type.ToUpperInvariant());
        }
        if (Types.Count > 0)
        {
            SelectedType = Types[0];
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (IsBusy)
        {
            return;
        }
        ErrorMessage = "";
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "请输入隧道名称。";
            return;
        }
        if (!int.TryParse(LocalPortText, out var localPort) || localPort is < 1 or > 65535)
        {
            ErrorMessage = "请输入有效的本地端口（1-65535）。";
            return;
        }
        if (Types.Count == 0)
        {
            ErrorMessage = "当前节点没有可用的隧道类型。";
            return;
        }

        IsBusy = true;
        try
        {
            var response = await _session.Api.CreateTunnelAsync(new CreateTunnelRequest
            {
                Type = SelectedType.ToLowerInvariant(),
                Name = Name.Trim(),
                LocalIp = string.IsNullOrWhiteSpace(LocalIp) ? "127.0.0.1" : LocalIp.Trim(),
                LocalPort = localPort,
            });
            if (!string.IsNullOrWhiteSpace(response.FrpcConfig))
            {
                _session.TunnelConfigs.Save(response.Tunnel.Id, response.FrpcConfig);
            }
            Created?.Invoke(this, response);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
