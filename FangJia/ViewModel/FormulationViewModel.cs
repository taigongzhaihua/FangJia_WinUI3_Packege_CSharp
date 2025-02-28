//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using FangJia.Common;
using FangJia.DataAccess;
using Microsoft.UI.Dispatching;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FangJia.ViewModel;

public partial class FormulationViewModel(FormulationManager formulationManager) : ObservableObject
{
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);
    internal readonly Logger Logger = LogManager.GetCurrentClassLogger();
    [ObservableProperty] public partial ObservableCollection<FormulationCategory> Categories { get; set; } = [];
    [ObservableProperty] public partial FormulationCategory? SelectedCategory { get; set; }
    [ObservableProperty] public partial Formulation? SelectedFormulation { get; set; }
    [ObservableProperty] public partial bool IsCategoryLoading { get; set; } = false;
    [ObservableProperty] public partial ObservableCollection<string> SearchWords { get; set; } = [];
    [ObservableProperty] public partial Dictionary<string, FormulationCategory> SearchDictionary { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<FormulationCategory> SecondCategories { get; set; } = [];
    [ObservableProperty] public partial bool IsFormulationSelected { get; set; } = false;
    [ObservableProperty] public partial Formulation? LastSelectedFormulation { get; set; }
    [ObservableProperty] public partial FormulationComposition? SelectedComposition { get; set; }
    public bool Flag = false;

    [SuppressMessage("ReSharper", "AsyncVoidMethod")]
    public async Task LoadCategoriesAsync(DispatcherQueue dispatcherQueue)
    {
        if (!await _loadSemaphore.WaitAsync(0)) return;
        try
        {
            await dispatcherQueue.EnqueueAsync(() =>
            {
                SearchWords = [];
                Categories.Clear();
                IsCategoryLoading = true;
            });

            await foreach (var category in formulationManager.GetFirstCategoriesAsync())
            {
                await foreach (var secondCategory in formulationManager.GetSecondCategoriesAsync(category.Name))
                {
                    await dispatcherQueue.EnqueueAsync(() => SecondCategories.Add(secondCategory));
                    await foreach (var formulation in formulationManager.GetFormulationsAsync(secondCategory.Id))
                    {
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
            Logger.Error(e);
            throw;
        }
        finally
        {
            _loadSemaphore.Release();
            await dispatcherQueue.EnqueueAsync(() => IsCategoryLoading = false);
        }
    }

    async partial void OnSelectedCategoryChanged(FormulationCategory? value)
    {
        try
        {
            LastSelectedFormulation = SelectedFormulation;
            if (value != null)
            {
                if (value.IsCategory)
                {
                    return;
                }

                var x = await formulationManager.GetFormulationByIdAsync(value.Id);
                if (LastSelectedFormulation != null && x != null && x.CategoryId != LastSelectedFormulation.CategoryId)
                {
                    Flag = true;
                }

                // 异步等待500毫秒
                await Task.Delay(200);

                // 延迟后再更新 SelectedFormulation
                SelectedFormulation = x;
                IsFormulationSelected = true;

                // 后续异步操作也可以继续使用 await
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
            }
            else
            {
                SelectedFormulation = null;
                IsFormulationSelected = false;
            }
        }
        catch (Exception e)
        {
            Logger.Error(e);
        }
    }


    [RelayCommand]
    public void UpdateFormulation(object key)
    {
        try
        {
            if (SelectedFormulation == null) return;
            var s = key as string;
            switch (s)
            {
                case "Name":
                    _ = formulationManager.UpdateFormulationAsync(SelectedFormulation.Id,
                        ("Name", SelectedFormulation!.Name!));
                    _ = Categories.Any(category => UpdateCategoryName(SelectedFormulation.Id, false, ref category));
                    break;
                case "CategoryId":
                    _ = formulationManager.UpdateFormulationAsync(SelectedFormulation.Id,
                        ("CategoryId", SelectedFormulation!.CategoryId.ToString()));
                    UpdateFormulationCategory(SelectedFormulation.Id, SelectedFormulation.CategoryId);
                    break;
                case "Usage":
                    _ = formulationManager.UpdateFormulationAsync(SelectedFormulation.Id,
                        ("Usage", SelectedFormulation!.Usage!));
                    break;
                case "Effect":
                    _ = formulationManager.UpdateFormulationAsync(SelectedFormulation.Id,
                        ("Effect", SelectedFormulation!.Effect!));
                    break;
                case "Indication":
                    _ = formulationManager.UpdateFormulationAsync(SelectedFormulation.Id,
                        ("Indication", SelectedFormulation!.Indication!));
                    break;
                case "Disease":
                    _ = formulationManager.UpdateFormulationAsync(SelectedFormulation.Id,
                        ("Disease", SelectedFormulation!.Disease!));
                    break;
                case "Application":
                    _ = formulationManager.UpdateFormulationAsync(SelectedFormulation.Id,
                        ("Application", SelectedFormulation!.Application!));
                    break;
                case "Supplement":
                    _ = formulationManager.UpdateFormulationAsync(SelectedFormulation.Id,
                        ("Supplement", SelectedFormulation!.Supplement!));
                    break;
                case "Song":
                    _ = formulationManager.UpdateFormulationAsync(SelectedFormulation.Id,
                        ("Song", SelectedFormulation!.Song!));
                    break;
                case "Notes":
                    _ = formulationManager.UpdateFormulationAsync(SelectedFormulation.Id,
                        ("Notes", SelectedFormulation!.Notes!));
                    break;
                case "Source":
                    _ = formulationManager.UpdateFormulationAsync(SelectedFormulation.Id,
                        ("Source", SelectedFormulation!.Source!));
                    break;
                default:
                    return;
            }
        }
        catch (Exception e)
        {
            Logger.Error(e);
        }
    }

    #region UpdateFormulation辅助方法

    private bool UpdateCategoryName(int id, bool isCategory, ref FormulationCategory category)
    {
        try
        {
            if (category.IsCategory != isCategory)
                return category.Children.Any(child => UpdateCategoryName(id, isCategory, ref child));
            if (category.Id != id) return false;
            category.Name = SelectedFormulation?.Name ?? string.Empty;
            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e);
            throw;
        }
    }

    private void UpdateFormulationCategory(int formulationId, int newCategoryId)
    {
        try
        {
            FormulationCategory? oldCategory = null;
            FormulationCategory? newCategory = null;
            FormulationCategory? target = null;
            if (!Categories.Any(category =>
                    FindCategoryFromCategory(formulationId, false, ref category!, out oldCategory, out target))) return;

            oldCategory?.Children.Remove(target!);
            if (Categories.Any(category =>
                    FindCategoryFromCategory(newCategoryId, true, ref category!, out _, out newCategory)))
            {
                newCategory?.Children.Add(target!);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e);
            throw;
        }
    }

    private bool FindCategoryFromCategory(
        int id, bool isCategory, ref FormulationCategory category,
        out FormulationCategory? parent, out FormulationCategory? target)
    {
        try
        {
            if (category.IsCategory != isCategory || category.Id != id)
            {
                foreach (var t in category.Children)
                {
                    var child = t;
                    if (!FindCategoryFromCategory(id, isCategory, ref child, out parent, out target)) continue;
                    parent ??= category;
                    return true;
                }
            }
            else if (category.Id == id)
            {
                parent = null;
                target = category;
                return true;
            }

            parent = null;
            target = null;
            return false;
        }
        catch (Exception e)
        {
            Logger.Error(e);
            throw;
        }
    }

    #endregion

    [RelayCommand]
    public void InsertNewFormulationCompositions()
    {
        try
        {
            if (SelectedFormulation == null) return;
            var fc = new FormulationComposition
            {
                FormulationId = SelectedFormulation.Id,
                DrugId = 0,
                DrugName = "药物名称",
                Effect = "功效",
                Position = "",
                Notes = "备注"
            };
            fc.Id = formulationManager.InsertFormulationComposition(fc).Result;
            SelectedFormulation.Compositions?.Add(fc);
        }
        catch (Exception e)
        {
            Logger.Error(e);
        }
    }

    [RelayCommand]
    public void DeleteFormulationCompositions()
    {
        try
        {
            if (SelectedFormulation == null) return;
            if (SelectedComposition == null) return;
            _ = formulationManager.DeleteFormulationComposition(SelectedComposition.Id);
            SelectedFormulation.Compositions?.Remove(SelectedComposition);
        }
        catch (Exception e)
        {
            Logger.Error(e);
        }
    }

    [RelayCommand]
    public async Task DeleteCategory()
    {
        try
        {
            if (SelectedCategory == null) return;
            await DeleteCategoryFromDatabase(SelectedCategory);
            if (App.MainDispatcherQueue != null)
                await LoadCategoriesAsync(App.MainDispatcherQueue);
        }
        catch (Exception e)
        {
            Logger.Error(e);
        }
    }

    private async Task DeleteCategoryFromDatabase(FormulationCategory? category)
    {
        try
        {
            if (category == null)
                return;

            if (category.IsCategory)
            {
                // 并行删除所有子分类
                var deleteTasks = category.Children.Select(DeleteCategoryFromDatabase);
                await Task.WhenAll(deleteTasks);

                // 删除当前分类
                if (category.Id >= 0)
                {
                    await formulationManager.DeleteCategory(category.Id);
                    return;
                }
            }

            // 对于非分类的情况或 Id 小于0的情况
            await formulationManager.DeleteFormulation(category.Id);
        }
        catch (Exception e)
        {
            Logger.Error(e);
        }
    }

    [RelayCommand]
    public async Task InsertCategoryAndFormulation(object obj)
    {
        try
        {
            switch (obj)
            {
                case (int id, string name):
                    var f = new Formulation
                    {
                        Name = name,
                        CategoryId = id,
                        Usage = "",
                        Effect = "",
                        Indication = "",
                        Disease = "",
                        Application = "",
                        Supplement = "",
                        Song = "",
                        Notes = "",
                        Source = ""
                    };
                    var insertFormulationId = await formulationManager.InsertFormulationAsync(f);
                    if (App.MainDispatcherQueue != null)
                    {
                        await LoadCategoriesAsync(App.MainDispatcherQueue);
                        await App.MainDispatcherQueue.EnqueueAsync(() =>
                        {
                            FormulationCategory? newFormulation = null;
                            var categoriesSnapshot = Categories.ToList();
                            if (categoriesSnapshot.Any(category =>
                                    FindCategoryFromCategory(insertFormulationId, false, ref category!, out _,
                                        out newFormulation)))
                            {
                                SelectedCategory = newFormulation;
                            }
                        });
                    }

                    break;
                case (string firstCategory, string secondCategory):
                    Logger.Error(firstCategory, secondCategory);
                    var categoryId = await formulationManager.InsertCategoryAsync(firstCategory, secondCategory);
                    if (App.MainDispatcherQueue != null)
                    {
                        await LoadCategoriesAsync(App.MainDispatcherQueue);
                        App.MainDispatcherQueue.TryEnqueue(() =>
                        {
                            FormulationCategory? newCategory = null;
                            var categoriesSnapshot = Categories.ToList();
                            if (categoriesSnapshot.Any(category =>
                                    FindCategoryFromCategory(categoryId, true, ref category!, out _, out newCategory)))
                            {
                                SelectedCategory = newCategory;
                            }

                        });
                    }

                    break;
            }
        }
        catch (Exception e)
        {
            Logger.Error(e);
        }
    }
}