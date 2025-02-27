using CommunityToolkit.Mvvm.ComponentModel;

namespace FangJia.ViewModel;

public partial class DrugViewModel : ObservableObject
{
    [ObservableProperty] public partial bool IsDrugSelected { get; set; } = true;
}