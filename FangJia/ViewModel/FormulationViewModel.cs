using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI;
using FangJia.Common;
using FangJia.DataAccess;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.ObjectModel;
using System.Threading;

namespace FangJia.ViewModel;
public partial class FormulationViewModel(FormulationManager formulationManager) : ObservableObject
{
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    [ObservableProperty] private ObservableCollection<FormulationCategory> _categories = [];
    [ObservableProperty] private FormulationCategory? _selectedCategory;
    [ObservableProperty] private Formulation? _selectedFormulation;


    public async void LoadCategoriesAsync(DispatcherQueue dispatcherQueue)
    {

        try
        {
            await dispatcherQueue.EnqueueAsync(() => Categories.Clear());

            await foreach (var category in formulationManager.GetFirstCategoriesAsync())
            {
                await foreach (var secondCategory in formulationManager.GetSecondCategoriesAsync(category.Name))
                {
                    await foreach (var formulation in formulationManager.GetFormulationsAsync(secondCategory.Id))
                    {
                        secondCategory.Children.Add(formulation);
                    }
                    category.Children.Add(secondCategory);
                }

                await dispatcherQueue.EnqueueAsync(() =>
                {
                    Categories.Add(category);
                });
            }

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}