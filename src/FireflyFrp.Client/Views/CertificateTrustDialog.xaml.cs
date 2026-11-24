using System.Net.Security;
using System.Windows;
using FireflyFrp.Client.Services;
using Wpf.Ui.Controls;

namespace FireflyFrp.Client.Views;

public partial class CertificateTrustDialog : FluentWindow
{
    public CertificateTrustDialog(CertificateTrustRequest request)
    {
        InitializeComponent();
        HostValue.Text = request.Host;
        SubjectValue.Text = request.Subject;
        IssuerValue.Text = request.Issuer;
        ValidityValue.Text =
            $"{request.NotBefore:yyyy-MM-dd HH:mm} 至 {request.NotAfter:yyyy-MM-dd HH:mm}";
        FingerprintValue.Text = FormatFingerprint(request.Sha256);
        ErrorsValue.Text = DescribeErrors(request.Errors);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Trust_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private static string FormatFingerprint(string value)
    {
        var parts = new List<string>();
        for (var index = 0; index + 1 < value.Length; index += 2)
        {
            parts.Add(value.Substring(index, 2));
        }
        return string.Join(":", parts);
    }

    private static string DescribeErrors(SslPolicyErrors errors)
    {
        var parts = new List<string>();
        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
        {
            parts.Add("服务器没有提供证书");
        }
        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
        {
            parts.Add("证书主机名与服务器地址不匹配");
        }
        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
        {
            parts.Add("证书链不受信任，可能是自签名证书");
        }
        return parts.Count == 0 ? "未知证书错误" : string.Join("；", parts);
    }
}
