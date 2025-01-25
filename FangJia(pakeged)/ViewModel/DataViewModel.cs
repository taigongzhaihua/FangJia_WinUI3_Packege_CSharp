using CommunityToolkit.Mvvm.ComponentModel;
using FangJia.Common;
using FangJia.Helpers;
using System.Collections.ObjectModel;

namespace FangJia.ViewModel
{
    public partial class DataViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<Category> _data =
        [
            (NavigationHelper.Categorizes["Formulation"] as Category)!,
            (NavigationHelper.Categorizes["Drug"] as Category)!,
            (NavigationHelper.Categorizes["Classic"] as Category)!,
            (NavigationHelper.Categorizes["Case"] as Category)!
        ];
    }
}
