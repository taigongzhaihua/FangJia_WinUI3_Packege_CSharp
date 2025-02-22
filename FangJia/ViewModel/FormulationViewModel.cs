using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI;
using FangJia.Common;
using FangJia.DataAccess;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace FangJia.ViewModel;
public partial class FormulationViewModel(FormulationManager formulationManager) : ObservableObject
{
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);
    [ObservableProperty] public partial ObservableCollection<FormulationCategory> Categories { get; set; } = [];
    [ObservableProperty] public partial FormulationCategory? SelectedCategory { get; set; }
    [ObservableProperty] public partial Formulation? SelectedFormulation { get; set; }
    [ObservableProperty] public partial bool IsCategoryLoading { get; set; } = false;
    [ObservableProperty] public partial List<string> SearchWords { get; set; } = [];
    [ObservableProperty] public partial Dictionary<string, FormulationCategory> SearchDictionary { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<FormulationCategory> SecondCategories { get; set; } = [];
    [ObservableProperty] public partial bool IsFormulationSelected { get; set; } = false;



    [SuppressMessage("ReSharper", "AsyncVoidMethod")]
    public async void LoadCategoriesAsync(DispatcherQueue dispatcherQueue)
    {
        if (!await _loadSemaphore.WaitAsync(0)) return;
        try
        {
            await dispatcherQueue.EnqueueAsync(() =>
            {
                Categories.Clear();
                SearchWords.Clear();
                IsCategoryLoading = true;
            });

            await foreach (var category in formulationManager.GetFirstCategoriesAsync())
            {
                await foreach (var secondCategory in formulationManager.GetSecondCategoriesAsync(category.Name))
                {
                    await dispatcherQueue.EnqueueAsync(() => SecondCategories.Add(secondCategory));
                    secondCategory.Parent = category; // 设置父节点
                    await foreach (var formulation in formulationManager.GetFormulationsAsync(secondCategory.Id))
                    {
                        formulation.Parent = secondCategory; // 设置父节点
                        secondCategory.Children.Add(formulation);
                        SearchWords.Add(formulation.Name);
                        SearchDictionary[formulation.Name] = formulation; // 添加到字典
                    }
                    category.Children.Add(secondCategory);
                }
                await dispatcherQueue.EnqueueAsync(() => Categories.Add(category));
            }

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        finally
        {

            _loadSemaphore.Release();
            await dispatcherQueue.EnqueueAsync(() => IsCategoryLoading = false);
        }
    }

    partial void OnSelectedCategoryChanged(FormulationCategory? value)
    {
        if (value != null)
        {
            if (value.IsCategory)
            {
                return;
            }
            SelectedFormulation = formulationManager.GetFormulationByIdAsync(value.Id).Result;
            IsFormulationSelected = true;
            Task.Run(async () =>
            {
                var fcs = new List<FormulationComposition>();
                await foreach (var fc in formulationManager.GetFormulationCompositionsAsync(value.Id))
                {
                    fcs.Add(fc);
                }

                App.MainDispatcherQueue?.TryEnqueue(() =>
                {
                    SelectedFormulation?.Compositions?.Clear();
                    foreach (var fc in fcs)
                    {
                        SelectedFormulation?.Compositions?.Add(fc);
                    }
                });

                var formulationImage = await formulationManager.GetFormulationImageAsync(value.Id);
                App.MainDispatcherQueue?.TryEnqueue(() =>
                {
                    if (formulationImage != null) SelectedFormulation!.FormulationImage = formulationImage;
                });
            });

        }
        else
        {
            SelectedFormulation = null;
            IsFormulationSelected = false;
        }
    }
}