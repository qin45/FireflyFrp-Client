using System.Windows;
using Wpf.Ui.Controls;

namespace QZ.Frp.Client.Views;

public enum CloseActionChoice
{
    Cancel,
    Exit,
    Tray,
}

public partial class CloseActionDialog : FluentWindow
{
    public CloseActionChoice Choice { get; private set; } = CloseActionChoice.Cancel;

    public bool Remember => RememberCheck.IsChecked == true;

    public CloseActionDialog()
    {
        InitializeComponent();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        Choice = ExitRadio.IsChecked == true
            ? CloseActionChoice.Exit
            : CloseActionChoice.Tray;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Choice = CloseActionChoice.Cancel;
        DialogResult = false;
    }
}
