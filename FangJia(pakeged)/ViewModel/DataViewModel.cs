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
            (Helper.Categorizes["Formulation"] as Category)!,
            (Helper.Categorizes["Drug"] as Category)!,
            (Helper.Categorizes["Classic"] as Category)!,
            (Helper.Categorizes["Case"] as Category)!
        ];
    }
}
