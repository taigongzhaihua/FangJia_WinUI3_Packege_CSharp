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

namespace FangJia.ViewModel;
public partial class FormulationViewModel(FormulationManager formulationManager) : ObservableObject
{
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    [ObservableProperty] private ObservableCollection<FormulationCategory> _categories = [];
    [ObservableProperty] private FormulationCategory? _selectedCategory;
    [ObservableProperty] private Formulation? _selectedFormulation;
    [ObservableProperty] private bool? _isCategoryLoading = false;
    [ObservableProperty] private List<string> _searchWords = [];
    [ObservableProperty] private Dictionary<string, FormulationCategory> _searchDictionary = [];

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
            });

            await foreach (var category in formulationManager.GetFirstCategoriesAsync())
            {
                await foreach (var secondCategory in formulationManager.GetSecondCategoriesAsync(category.Name))
                {
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
        }
    }
}