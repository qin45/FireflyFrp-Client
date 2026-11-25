using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FireflyFrp.Client.ViewModels;

public sealed partial class EditTunnelViewModel : ObservableObject
{
    private readonly TunnelCardViewModel _card;

    public event EventHandler? Saved;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _localIp = "127.0.0.1";
    [ObservableProperty] private string _localPortText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _errorMessage = "";

    public EditTunnelViewModel(TunnelCardViewModel card)
    {
        _card = card;
        Name = card.TunnelName;
        LocalIp = card.LocalIp;
        LocalPortText = card.LocalPort.ToString();
    }

    public string TypeLabel => _card.TypeLabel;

    [RelayCommand]
    private async Task SaveAsync()
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

        IsBusy = true;
        try
        {
            await _card.UpdateDetailsAsync(Name.Trim(), LocalIp, localPort);
            Saved?.Invoke(this, EventArgs.Empty);
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
