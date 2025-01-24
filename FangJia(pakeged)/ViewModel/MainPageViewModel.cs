using CommunityToolkit.Mvvm.ComponentModel;
using FangJia.Common;
using System.Collections.ObjectModel;
using Helper = FangJia.Helpers.Helper;

namespace FangJia.ViewModel;

public partial class MainPageViewModel : ObservableObject
{
    [ObservableProperty] private bool _isFullScreen;
    [ObservableProperty] private ObservableCollection<Category> _pageHeader = [];
    [ObservableProperty] private ObservableCollection<CategoryBase> _menuFolders = [];
    [ObservableProperty] private ObservableCollection<CategoryBase> _footFolders = [];

    public MainPageViewModel()
    {
        var dataCategory = Helper.Categorizes["Data"] as Category;
        dataCategory!.Children =
        [
            Helper.Categorizes["Formulation"],
            Helper.Categorizes["Drug"],
            Helper.Categorizes["Classic"],
            Helper.Categorizes["Case"]
        ];
        MenuFolders =
        [
            Helper.Categorizes["Home"],
            dataCategory
        ];
        FootFolders =
        [
            Helper.Categorizes["About"],
            Helper.Categorizes["Separator"]
        ];
    }

}