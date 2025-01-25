using CommunityToolkit.Mvvm.ComponentModel;
using FangJia.Common;
using FangJia.Helpers;
using System.Collections.ObjectModel;

namespace FangJia.ViewModel;

public partial class MainPageViewModel : ObservableObject
{
    [ObservableProperty] private bool _isFullScreen;
    [ObservableProperty] private ObservableCollection<Category> _pageHeader = [];
    [ObservableProperty] private ObservableCollection<CategoryBase> _menuFolders = [];
    [ObservableProperty] private ObservableCollection<CategoryBase> _footFolders = [];

    public MainPageViewModel()
    {
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

}