using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FangJia.Common;
using FangJia.Helpers;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;

namespace FangJia.ViewModel;

public partial class MainPageViewModel : ObservableObject
{
    [ObservableProperty] private bool _isFullScreen;
    [ObservableProperty] private ObservableCollection<Category> _pageHeader = [];
    [ObservableProperty] private ObservableCollection<CategoryBase> _menuFolders = [];
    [ObservableProperty] private ObservableCollection<CategoryBase> _footFolders = [];
    [ObservableProperty] private bool? _isWindowVisible;
    [ObservableProperty] private string _showOrHideMenuText = "最小化到托盘";
    public MainPageViewModel()
    {
        IsWindowVisible = true;
        var dataCategory = NavigationHelper.Categorizes["Data"] as Category;
        dataCategory!.Children =
        [
            NavigationHelper.Categorizes["Formulation"],
            NavigationHelper.Categorizes["Drug"],
            NavigationHelper.Categorizes["Classic"],
            NavigationHelper.Categorizes["Case"]
        ];
        MenuFolders =
        [
            NavigationHelper.Categorizes["Home"],
            dataCategory
        ];
        FootFolders =
        [
            NavigationHelper.Categorizes["About"],
            NavigationHelper.Categorizes["Separator"]
        ];
    }

    partial void OnIsWindowVisibleChanged(bool? value)
    {
        ShowOrHideMenuText = value == true ? "最小化到托盘" : "显示主窗口";
    }

    [RelayCommand]
    public void ShowHideWindow(object sender)
    {
        var window = App.Window;
        if (window == null)
        {
            return;
        }

        if (window.Visible)
        {
            window.Hide();
        }
        else
        {
            window.Show();
            window.Activate();

        }
        IsWindowVisible = window.Visible;
    }


    [RelayCommand]
    public static void ExitApplication()
    {
        App.HandleClosedEvents = false;
        Application.Current.Exit();
    }
}