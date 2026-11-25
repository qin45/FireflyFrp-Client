using System.Windows;
using Wpf.Ui.Controls;

namespace FireflyFrp.Client.Views;

public partial class EditTunnelWindow : FluentWindow
{
    public EditTunnelWindow()
    {
        InitializeComponent();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
