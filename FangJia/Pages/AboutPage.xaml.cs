// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.System.Profile;

namespace FangJia.Pages;

/// <summary>
/// 一个可以单独使用或在 Frame 中导航到的空页面。
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Local")]
public sealed partial class AboutPage
{
    public AboutPage()
    {
        InitializeComponent();
    }

    private string AppVersion
    {
        get
        {
            var version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
    }

    private string AppName => Package.Current.DisplayName;

    private string AppPublisher => Package.Current.PublisherDisplayName;

    private string AppDescription => Properties.Resource.AppDescription;

    private string AppArchitecture => Package.Current.Id.Architecture.ToString();

    private string OsVersion
    {
        get
        {
            var version = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
            var versionNumbers = ulong.Parse(version);
            var major = (versionNumbers & 0xFFFF000000000000L) >> 48;
            var minor = (versionNumbers & 0x0000FFFF00000000L) >> 32;
            var build = (versionNumbers & 0x00000000FFFF0000L) >> 16;
            var revision = (versionNumbers & 0x000000000000FFFFL);
            return $"{major}.{minor}.{build}.{revision}";
        }
    }

    private string GitHubUri => Properties.Resource.GithubUri;
    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        OpenUrl(GitHubUri);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            // 打开默认浏览器并导航到指定网址
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            // 处理异常
            Console.WriteLine($@"无法打开网址: {ex.Message}");
        }
    }

    private string EMail => Properties.Resource.EMail;


    private void CopyEMail(object sender, RoutedEventArgs e)
    {
        var dataPackage = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy
        };
        dataPackage.SetText(EMail);
        Clipboard.SetContent(dataPackage);
    }
}