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
using Microsoft.UI.Xaml;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FangJia.ViewModel;

public partial class FormulationViewModel : ObservableObject
{
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private bool _isUpdatingFromCategoryChange;

    // 添加内存缓存
    private readonly ConcurrentDictionary<int, Formulation> _formulationCache = new();
    private readonly ConcurrentDictionary<int, FormulationImage> _imageCache = new();

    #region 属性

    [ObservableProperty] public partial ObservableCollection<FormulationCategory> Categories { get; set; } = [];
    [ObservableProperty] public partial FormulationCategory? SelectedCategory { get; set; }
    [ObservableProperty] public partial Formulation? SelectedFormulation { get; set; }
    [ObservableProperty] public partial bool IsCategoryLoading { get; set; }
    [ObservableProperty] public partial ObservableCollection<string> SearchWords { get; set; } = [];
    [ObservableProperty] public partial Dictionary<string, FormulationCategory> SearchDictionary { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<FormulationCategory> SecondCategories { get; set; } = [];
    [ObservableProperty] public partial bool IsFormulationSelected { get; set; }
    [ObservableProperty] public partial FormulationComposition? SelectedComposition { get; set; }

    public Formulation? LastSelectedFormulation { get; set; }

    // 用于防止循环更新的标志
    public bool Flag { get; set; }

    // 事件
    public event RoutedEventHandler? SelectedFormulaChanged;
    public event RoutedEventHandler? FormulaImageChanged;

    #endregion

    public FormulationViewModel()
    {
        _logger.Info($"{typeof(FormulationViewModel).FullName}初始化");

    }

    #region 数据加载

    [SuppressMessage("ReSharper", "AsyncVoidMethod")]
    public async Task LoadCategoriesAsync(DispatcherQueue dispatcherQueue)
    {
        _logger.Info("开始加载分类数据");
        var stopwatch = Stopwatch.StartNew();

        if (!await _loadSemaphore.WaitAsync(0))
        {
            _logger.Debug("加载分类操作已在进行中，跳过重复加载");
            return;
        }

        try
        {
            await dispatcherQueue.EnqueueAsync(() =>
            {
                SearchWords.Clear();
                Categories.Clear();
                SearchDictionary.Clear();
                IsCategoryLoading = true;
                _logger.Debug("清空现有数据并设置加载状态");
            });

            // 使用优化的批量加载数据方法
            var categoriesTask = FormulationManager.LoadAllCategoriesAsync();
            var formulationsTask = FormulationManager.LoadAllFormulationsBasicAsync();

            // 同时执行两个任务
            await Task.WhenAll(categoriesTask, formulationsTask);

            var categories = await categoriesTask;
            var formulations = await formulationsTask;

            var categoryCount = 0;
            var secondCategoryCount = 0;
            var formulationCount = 0;

            // 使用批量加载的数据构建UI结构
            var categoryList = new List<FormulationCategory>();

            if (categories?.Keys != null)
                foreach (var firstCategory in categories.Keys)
                {
                    categoryCount++;
                    _logger.Trace($"加载一级分类: {firstCategory}");

                    // 创建一级分类节点
                    var firstCategoryNode = new FormulationCategory(-categoryCount, firstCategory, true);

                    // 添加二级分类
                    foreach (var secondCategory in categories[firstCategory])
                    {
                        secondCategoryCount++;
                        _logger.Trace($"加载二级分类: {secondCategory.Name} (ID={secondCategory.Id})");

                        await dispatcherQueue.EnqueueAsync(() => SecondCategories.Add(secondCategory));

                        // 获取该分类下的所有方剂
                        if (formulations!.TryGetValue(secondCategory.Id, out var categoryFormulations))
                        {
                            foreach (var formulation in categoryFormulations)
                            {
                                formulationCount++;
                                _logger.Trace($"加载方剂: {formulation.Name} (ID={formulation.Id})");

                                secondCategory.Children.Add(formulation);
                                await dispatcherQueue.EnqueueAsync(() =>
                                {
                                    SearchWords.Add(formulation.Name);
                                    SearchDictionary[formulation.Name] = formulation;
                                });
                            }
                        }

                        firstCategoryNode.Children.Add(secondCategory);
                    }

                    categoryList.Add(firstCategoryNode);
                }

            // 更新UI
            await dispatcherQueue.EnqueueAsync(() =>
            {
                foreach (var category in categoryList)
                {
                    Categories.Add(category);
                }
            });

            stopwatch.Stop();
            _logger.Info($"分类数据加载完成: {categoryCount} 个一级分类, {secondCategoryCount} 个二级分类, {formulationCount} 个方剂, 耗时 {stopwatch.ElapsedMilliseconds} ms");
        }
        catch (Exception e)
        {
            _logger.Error($"加载分类数据失败: {e.Message}", e);
            _logger.Debug($"加载失败堆栈: {e.StackTrace}");
        }
        finally
        {
            _loadSemaphore.Release();
            await dispatcherQueue.EnqueueAsync(() => IsCategoryLoading = false);
        }
    }

    #endregion

    #region 属性变更处理

    [SuppressMessage("ReSharper", "AsyncVoidMethod")]
    async partial void OnSelectedCategoryChanged(FormulationCategory? value)
    {
        // 防止循环更新
        if (_isUpdatingFromCategoryChange)
        {
            _logger.Trace("检测到循环更新，跳过处理");
            return;
        }

        _isUpdatingFromCategoryChange = true;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (value == null)
            {
                _logger.Debug("选中分类为空，清除当前选择");
                SelectedFormulation = null;
                IsFormulationSelected = false;
                return;
            }

            _logger.Debug($"分类选择变更: {value.Name} (ID={value.Id}, 类型={(value.IsCategory ? "分类" : "方剂")})");

            // 处理上一个选中的方剂
            if (value is { IsCategory: false } && SelectedFormulation is { Name: not null })
            {
                LastSelectedFormulation = SelectedFormulation;
                _logger.Debug($"记录上一个选中的方剂: {LastSelectedFormulation.Name} (ID={LastSelectedFormulation.Id})");
            }

            // 如果选中的是分类而非方剂，则退出
            if (value.IsCategory)
            {
                _logger.Debug("选中的是分类，不加载方剂详情");
                return;
            }

            // 尝试从缓存获取方剂详情
            Formulation? formulation;
            if (_formulationCache.TryGetValue(value.Id, out var cachedFormulation))
            {
                _logger.Debug($"从缓存获取方剂详情: ID={value.Id}");
                formulation = cachedFormulation;
            }
            else
            {
                // 获取方剂详情
                _logger.Debug($"开始加载方剂详情: ID={value.Id}");
                formulation = await FormulationManager.GetFormulationByIdAsync(value.Id);

                // 添加到缓存
                if (formulation != null)
                {
                    _formulationCache[value.Id] = formulation;
                }
            }

            // 检查是否需要设置标志
            if ((formulation != null && LastSelectedFormulation != null &&
                 formulation.CategoryId != LastSelectedFormulation.CategoryId) ||
                LastSelectedFormulation == null)
            {
                Flag = true;
                _logger.Debug("设置Flag标志，防止循环更新");
            }

            if (LastSelectedFormulation != null)
            {
                await Task.Delay(100);
            }

            // 更新UI并触发事件
            SelectedFormulation = formulation;
            IsFormulationSelected = true;
            _logger.Info($"方剂加载完成: {formulation?.Name} (ID={formulation?.Id})");

            SelectedFormulaChanged?.Invoke(this, new RoutedEventArgs());
            _logger.Debug($@"触发{typeof(FormulationViewModel).FullName}.SelectedFormulaChanged事件");

            // 并行加载方剂组成和图片
            if (SelectedFormulation != null)
            {
                var compositionTask = LoadFormulationCompositionsAsync(value.Id);
                var imageTask = LoadFormulationImageAsync(value.Id);

                // 等待组成加载完成
                await compositionTask;

                // 等待图片加载完成并触发事件
                await imageTask;
                _logger.Debug("方剂图片加载完成");
                FormulaImageChanged?.Invoke(this, new RoutedEventArgs());
                _logger.Debug($"触发{typeof(FormulationViewModel).FullName}.FormulaImageChanged事件");
            }

            stopwatch.Stop();
            _logger.Info($"方剂详情加载完成，耗时 {stopwatch.ElapsedMilliseconds} ms");
        }
        catch (ArgumentNullException e)
        {
            _logger.Error($"方剂详情加载失败(参数错误): {e.Message}", e);
            _logger.Debug($"参数错误堆栈: {e.StackTrace}");
        }
        catch (Exception e)
        {
            _logger.Error($"方剂详情加载失败: {e.Message}", e);
            _logger.Debug($"异常堆栈: {e.StackTrace}");
        }
        finally
        {
            _isUpdatingFromCategoryChange = false;
        }
    }

    /// <summary>
    /// 加载方剂组成 - 优化版
    /// </summary>
    private async Task LoadFormulationCompositionsAsync(int formulationId)
    {
        _logger.Debug($"开始加载方剂组成: 方剂ID={formulationId}");

        try
        {
            if (SelectedFormulation != null)
            {
                // 清空现有组成
                SelectedFormulation.Compositions?.Clear();

                var compositionCount = 0;
                // 重新加载组成
                await foreach (var composition in FormulationManager.GetFormulationCompositionsAsync(formulationId))
                {
                    compositionCount++;
                    _logger.Trace($"加载方剂组成: {composition.DrugName} (ID={composition.Id})");
                    SelectedFormulation.Compositions?.Add(composition);
                }

                _logger.Debug($"方剂组成加载完成，共{compositionCount}个组成");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"加载方剂组成失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 加载方剂图片 - 优化版
    /// </summary>
    private async Task LoadFormulationImageAsync(int formulationId)
    {
        _logger.Debug($"开始加载方剂图片: 方剂ID={formulationId}");

        try
        {
            if (SelectedFormulation != null)
            {
                // 尝试从缓存获取图片
                if (_imageCache.TryGetValue(formulationId, out var cachedImage))
                {
                    _logger.Debug($"从缓存获取方剂图片: ID={formulationId}");
                    SelectedFormulation.FormulationImage = cachedImage;
                }
                else
                {
                    // 加载并缓存图片
                    var image = await FormulationManager.GetFormulationImageAsync(formulationId);
                    if (image != null)
                    {
                        _imageCache[formulationId] = image;
                        SelectedFormulation.FormulationImage = image;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"加载方剂图片失败: {ex.Message}", ex);
        }
    }

    #endregion

    #region 更新方法

    [RelayCommand]
    public void UpdateFormulation(object key)
    {
        if (SelectedFormulation == null)
        {
            _logger.Warn("尝试更新方剂，但未选中方剂");
            return;
        }

        var propertyName = key as string;
        if (string.IsNullOrEmpty(propertyName))
        {
            _logger.Warn($"更新方剂属性失败: 无效的属性名 '{key}'");
            return;
        }

        _logger.Info($"更新方剂属性: {SelectedFormulation.Name} (ID={SelectedFormulation.Id}), 属性={propertyName}");

        try
        {
            switch (propertyName)
            {
                case "Name":
                    _logger.Debug($"更新方剂名称: '{SelectedFormulation.Name}'");
                    _ = FormulationManager.UpdateFormulationAsync(SelectedFormulation.Id,
                        ("Name", SelectedFormulation.Name!));

                    // 更新分类树中的名称
                    foreach (var category in Categories)
                    {
                        var formulationCategory = category;
                        if (UpdateCategoryName(SelectedFormulation.Id, false, ref formulationCategory))
                        {
                            _logger.Debug("分类树中的方剂名称已更新");

                            // 更新搜索字典
                            UpdateSearchDictionary(SelectedFormulation.Id, SelectedFormulation.Name!);
                            break;
                        }
                    }
                    break;

                case "CategoryId":
                    var newCategoryId = SelectedFormulation.CategoryId;
                    _logger.Debug($"更新方剂分类: ID从{SelectedFormulation.CategoryId}");
                    _ = FormulationManager.UpdateFormulationAsync(SelectedFormulation.Id,
                        ("CategoryId", newCategoryId.ToString()));
                    UpdateFormulationCategory(SelectedFormulation.Id, newCategoryId);

                    // 更新缓存
                    if (_formulationCache.TryGetValue(SelectedFormulation.Id, out var cachedFormulation))
                    {
                        cachedFormulation.CategoryId = newCategoryId;
                    }
                    break;

                // 更新其他属性
                case "Usage":
                case "Effect":
                case "Indication":
                case "Disease":
                case "Application":
                case "Supplement":
                case "Song":
                case "Notes":
                case "Source":
                    var propertyValue = SelectedFormulation.GetType().GetProperty(propertyName)?.GetValue(SelectedFormulation) as string ?? string.Empty;
                    _logger.Debug($"更新方剂属性 {propertyName}: 新值长度={propertyValue.Length}字符");
                    _ = FormulationManager.UpdateFormulationAsync(SelectedFormulation.Id, (propertyName, propertyValue));

                    // 更新缓存
                    if (_formulationCache.TryGetValue(SelectedFormulation.Id, out var cached))
                    {
                        var property = cached.GetType().GetProperty(propertyName);
                        property?.SetValue(cached, propertyValue);
                    }
                    break;

                default:
                    _logger.Warn($"未知的方剂属性: {propertyName}");
                    return;
            }

            _logger.Info($"方剂属性 {propertyName} 更新成功");
        }
        catch (Exception e)
        {
            _logger.Error($"更新方剂属性 {propertyName} 失败: {e.Message}", e);
            _logger.Debug($"异常堆栈: {e.StackTrace}");
        }
    }

    /// <summary>
    /// 更新搜索字典中的方剂名称
    /// </summary>
    private void UpdateSearchDictionary(int formulationId, string newName)
    {
        // 找到旧的键值
        var oldKey = SearchDictionary.FirstOrDefault(x => x.Value.Id == formulationId).Key;
        if (oldKey != null && oldKey != newName)
        {
            // 如果名称已经改变，更新SearchWords和SearchDictionary
            var item = SearchDictionary[oldKey];
            SearchDictionary.Remove(oldKey);
            SearchDictionary[newName] = item;

            // 更新搜索词
            var index = SearchWords.IndexOf(oldKey);
            if (index >= 0)
            {
                SearchWords[index] = newName;
            }
        }
    }

    #endregion

    #region 分类操作辅助方法

    /// <summary>
    /// 更新分类树中的方剂名称
    /// </summary>
    private bool UpdateCategoryName(int id, bool isCategory, ref FormulationCategory category)
    {
        try
        {
            // 如果分类类型不匹配，递归查找子分类
            if (category.IsCategory != isCategory)
            {
                return category.Children.Any(child =>
                {
                    var childRef = child;
                    return UpdateCategoryName(id, isCategory, ref childRef);
                });
            }

            // 找到匹配项，更新名称
            if (category.Id == id)
            {
                var oldName = category.Name;
                category.Name = SelectedFormulation?.Name ?? string.Empty;
                _logger.Debug($"更新分类树中的名称: ID={id}, 旧名称='{oldName}', 新名称='{category.Name}'");
                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            _logger.Error($"更新分类名称失败: ID={id}, {e.Message}", e);
            _logger.Debug($"异常堆栈: {e.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// 更新方剂所属分类
    /// </summary>
    private void UpdateFormulationCategory(int formulationId, int newCategoryId)
    {
        try
        {
            _logger.Debug($"开始更新方剂分类: 方剂ID={formulationId}, 新分类ID={newCategoryId}");

            FormulationCategory? oldCategory = null;
            FormulationCategory? newCategory = null;
            FormulationCategory? target = null;

            // 查找要移动的方剂和它当前的分类
            var found = false;
            foreach (var category in Categories)
            {
                var categoryRef = category;
                if (FindCategoryFromCategory(formulationId, false, ref categoryRef, out oldCategory, out target))
                {
                    found = true;
                    _logger.Debug($"找到方剂: ID={formulationId}, 名称='{target?.Name}', 当前分类='{oldCategory?.Name}' (ID={oldCategory?.Id})");
                    break;
                }
            }

            if (!found)
            {
                _logger.Warn($"未找到方剂: ID={formulationId}");
                return;
            }

            // 从原分类中移除
            oldCategory?.Children.Remove(target!);
            _logger.Debug($"从原分类 '{oldCategory?.Name}' 移除方剂 '{target?.Name}'");

            // 查找目标分类并添加方剂
            found = false;
            foreach (var category in Categories)
            {
                var categoryRef = category;
                if (FindCategoryFromCategory(newCategoryId, true, ref categoryRef, out _, out newCategory))
                {
                    found = true;
                    _logger.Debug($"找到目标分类: ID={newCategoryId}, 名称='{newCategory?.Name}'");
                    break;
                }
            }

            if (!found)
            {
                _logger.Warn($"未找到目标分类: ID={newCategoryId}");
                return;
            }

            newCategory?.Children.Add(target!);
            _logger.Info($"方剂 '{target?.Name}' 已从分类 '{oldCategory?.Name}' 移动到分类 '{newCategory?.Name}'");
        }
        catch (Exception e)
        {
            _logger.Error($"更新方剂分类失败: 方剂ID={formulationId}, 新分类ID={newCategoryId}, {e.Message}", e);
            _logger.Debug($"异常堆栈: {e.StackTrace}");
        }
    }

    /// <summary>
    /// 在分类树中查找指定ID的分类或方剂
    /// </summary>
    private bool FindCategoryFromCategory(int id, bool isCategory, ref FormulationCategory category,
        out FormulationCategory? parent, out FormulationCategory? target)
    {
        // 初始化输出参数
        parent = null;
        target = null;

        try
        {
            // 检查当前节点是否匹配
            if (category.IsCategory == isCategory && category.Id == id)
            {
                target = category;
                _logger.Trace($"找到目标: ID={id}, 名称='{category.Name}', 类型={(isCategory ? "分类" : "方剂")}");
                return true;
            }

            // 递归查找子节点
            foreach (var child in category.Children)
            {
                var childRef = child;
                if (FindCategoryFromCategory(id, isCategory, ref childRef, out var childParent, out var childTarget))
                {
                    parent = childParent ?? category;
                    target = childTarget;
                    return true;
                }
            }

            return false;
        }
        catch (Exception e)
        {
            _logger.Error($"查找分类失败: ID={id}, {e.Message}", e);
            _logger.Debug($"异常堆栈: {e.StackTrace}");
            throw;
        }
    }

    #endregion

    #region 方剂组成操作

    [RelayCommand]
    public void InsertNewFormulationCompositions()
    {
        if (SelectedFormulation == null)
        {
            _logger.Warn("尝试插入方剂组成，但未选中方剂");
            return;
        }

        try
        {
            _logger.Info($"开始插入新方剂组成: 方剂={SelectedFormulation.Name} (ID={SelectedFormulation.Id})");

            // 创建新的方剂组成
            var composition = new FormulationComposition
            {
                FormulationId = SelectedFormulation.Id,
                DrugId = 0,
                DrugName = "药物名称",
                Effect = "功效",
                Position = "",
                Notes = "备注"
            };

            // 插入到数据库并获取ID
            composition.Id = FormulationManager.InsertFormulationComposition(composition).Result;
            _logger.Debug($"方剂组成已插入数据库: ID={composition.Id}");

            // 添加到UI集合
            SelectedFormulation.Compositions ??= [];
            SelectedFormulation.Compositions.Add(composition);
            _logger.Info($"方剂组成插入成功: ID={composition.Id}, 方剂={SelectedFormulation.Name}");
        }
        catch (Exception e)
        {
            _logger.Error($"插入方剂组成失败: 方剂ID={SelectedFormulation?.Id}, {e.Message}", e);
            _logger.Debug($"异常堆栈: {e.StackTrace}");
        }
    }

    [RelayCommand]
    public void DeleteFormulationCompositions()
    {
        if (SelectedFormulation == null)
        {
            _logger.Warn("尝试删除方剂组成，但未选中方剂");
            return;
        }

        if (SelectedComposition == null)
        {
            _logger.Warn("尝试删除方剂组成，但未选中组成项");
            return;
        }

        try
        {
            _logger.Info($"开始删除方剂组成: ID={SelectedComposition.Id}, 药物={SelectedComposition.DrugName}, 方剂={SelectedFormulation.Name}");

            // 从数据库删除
            _ = FormulationManager.DeleteFormulationComposition(SelectedComposition.Id);
            _logger.Debug($"方剂组成已从数据库删除: ID={SelectedComposition.Id}");

            // 从UI集合删除
            SelectedFormulation.Compositions?.Remove(SelectedComposition);
            _logger.Info($"方剂组成删除成功: ID={SelectedComposition.Id}, 药物={SelectedComposition.DrugName}");
        }
        catch (Exception e)
        {
            _logger.Error($"删除方剂组成失败: ID={SelectedComposition?.Id}, {e.Message}", e);
            _logger.Debug($"异常堆栈: {e.StackTrace}");
        }
    }

    #endregion

    #region 分类和方剂操作

    [RelayCommand]
    public async Task DeleteCategory()
    {
        if (SelectedCategory == null)
        {
            _logger.Warn("尝试删除分类，但未选中分类");
            return;
        }

        try
        {
            _logger.Info($"开始删除分类: {SelectedCategory.Name} (ID={SelectedCategory.Id}, 类型={(SelectedCategory.IsCategory ? "分类" : "方剂")})");
            var stopwatch = Stopwatch.StartNew();

            // 从数据库删除分类及其子项
            await DeleteCategoryFromDatabase(SelectedCategory);

            // 记录操作时间
            stopwatch.Stop();
            _logger.Info($"分类删除完成，耗时{stopwatch.ElapsedMilliseconds}ms");

            // 清除相关缓存
            ClearCaches();

            // 重新加载数据
            if (App.MainDispatcherQueue != null)
            {
                _logger.Debug("开始重新加载分类数据");
                await LoadCategoriesAsync(App.MainDispatcherQueue);
            }
        }
        catch (Exception e)
        {
            _logger.Error($"删除分类失败: {SelectedCategory.Name} (ID={SelectedCategory.Id}), {e.Message}", e);
            _logger.Debug($"异常堆栈: {e.StackTrace}");
        }
    }

    /// <summary>
    /// 清除所有本地缓存
    /// </summary>
    private void ClearCaches()
    {
        _formulationCache.Clear();
        _imageCache.Clear();
    }

    /// <summary>
    /// 递归删除分类及其所有子项
    /// </summary>
    private async Task DeleteCategoryFromDatabase(FormulationCategory? category)
    {
        if (category == null)
        {
            _logger.Warn("尝试删除空分类");
            return;
        }

        try
        {
            _logger.Debug($"删除分类: {category.Name} (ID={category.Id}, 类型={(category.IsCategory ? "分类" : "方剂")})");

            if (category.IsCategory)
            {
                _logger.Debug($"分类 '{category.Name}' 包含 {category.Children.Count} 个子项，开始递归删除");

                // 创建删除任务列表
                var deleteTasks = category.Children.Select(DeleteCategoryFromDatabase).ToList();

                // 为每个子项创建删除任务

                // 并行执行所有删除任务
                await Task.WhenAll(deleteTasks);
                _logger.Debug($"分类 '{category.Name}' 的所有子项已删除");

                // 删除当前分类
                if (category.Id >= 0)
                {
                    await FormulationManager.DeleteCategory(category.Id);
                    _logger.Debug($"分类 '{category.Name}' (ID={category.Id}) 已从数据库删除");
                    return;
                }
            }

            // 删除方剂
            await FormulationManager.DeleteFormulation(category.Id);
            _logger.Debug($"方剂 '{category.Name}' (ID={category.Id}) 已从数据库删除");
        }
        catch (Exception e)
        {
            _logger.Error($"从数据库删除分类失败: {category.Name} (ID={category.Id}), {e.Message}", e);
            _logger.Debug($"异常堆栈: {e.StackTrace}");
        }
    }

    [RelayCommand]
    public async Task InsertCategoryAndFormulation(object obj)
    {
        try
        {
            _logger.Info($"开始插入分类或方剂，参数类型: {obj.GetType().Name}");

            switch (obj)
            {
                // 新增方剂
                case (int id, string name):
                    _logger.Info($"新增方剂: 名称='{name}', 分类ID={id}");
                    await InsertNewFormulation(id, name);
                    break;

                // 新增分类
                case (string firstCategory, string secondCategory):
                    _logger.Info($"新增分类: 一级分类='{firstCategory}', 二级分类='{secondCategory}'");
                    await InsertNewCategory(firstCategory, secondCategory);
                    break;

                default:
                    _logger.Warn($"未知的参数类型: {obj.GetType().Name}");
                    break;
            }
        }
        catch (Exception e)
        {
            _logger.Error($"新增分类或方剂失败: {e.Message}", e);
            _logger.Debug($"异常堆栈: {e.StackTrace}");
        }
    }

    /// <summary>
    /// 插入新方剂 - 优化版
    /// </summary>
    private async Task InsertNewFormulation(int categoryId, string name)
    {
        _logger.Debug($"开始插入新方剂: 名称='{name}', 分类ID={categoryId}");
        var stopwatch = Stopwatch.StartNew();

        // 创建新方剂对象
        var formulation = new Formulation
        {
            Name = name,
            CategoryId = categoryId,
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

        // 插入到数据库
        var insertedId = await FormulationManager.InsertFormulationAsync(formulation);
        _logger.Debug($"方剂已插入数据库: ID={insertedId}, 名称='{name}'");

        // 添加到缓存
        formulation.Id = insertedId;
        _formulationCache[insertedId] = formulation;

        stopwatch.Stop();
        _logger.Info($"方剂 '{name}' 插入成功，ID={insertedId}，耗时{stopwatch.ElapsedMilliseconds}ms");

        // 重新加载数据并选择新方剂
        if (App.MainDispatcherQueue != null)
        {
            _logger.Debug("开始重新加载分类数据");
            await LoadCategoriesAsync(App.MainDispatcherQueue);

            await App.MainDispatcherQueue.EnqueueAsync(() =>
            {
                _logger.Debug($"查找并选择新插入的方剂: ID={insertedId}");
                // 查找并选择新插入的方剂
                foreach (var category in Categories)
                {
                    var categoryRef = category;
                    if (!FindCategoryFromCategory(insertedId, false, ref categoryRef, out _, out var newFormulation))
                        continue;
                    _logger.Debug($"找到新插入的方剂: '{newFormulation?.Name}' (ID={newFormulation!.Id})");
                    SelectedCategory = newFormulation;
                    _logger.Info($"已选中新插入的方剂: '{newFormulation.Name}'");
                    break;
                }
            });
        }
    }

    /// <summary>
    /// 插入新分类 - 优化版
    /// </summary>
    private async Task InsertNewCategory(string firstCategory, string secondCategory)
    {
        _logger.Debug($"开始插入新分类: 一级分类='{firstCategory}', 二级分类='{secondCategory}'");
        var stopwatch = Stopwatch.StartNew();

        // 插入到数据库
        var categoryId = await FormulationManager.InsertCategoryAsync(firstCategory, secondCategory);
        _logger.Debug($"分类已插入数据库: ID={categoryId}, 一级分类='{firstCategory}', 二级分类='{secondCategory}'");

        stopwatch.Stop();
        _logger.Info($"分类 '{secondCategory}' (归属于'{firstCategory}') 插入成功，ID={categoryId}，耗时{stopwatch.ElapsedMilliseconds}ms");

        // 重新加载数据并选择新分类
        if (App.MainDispatcherQueue != null)
        {
            _logger.Debug("开始重新加载分类数据");
            await LoadCategoriesAsync(App.MainDispatcherQueue);

            App.MainDispatcherQueue.TryEnqueue(() =>
            {
                _logger.Debug($"查找并选择新插入的分类: ID={categoryId}");
                // 查找并选择新插入的分类
                foreach (var category in Categories)
                {
                    var categoryRef = category;
                    if (!FindCategoryFromCategory(categoryId, true, ref categoryRef, out _, out var newCategory))
                        continue;
                    _logger.Debug($"找到新插入的分类: '{newCategory?.Name}' (ID={newCategory!.Id})");
                    SelectedCategory = newCategory;
                    _logger.Info($"已选中新插入的分类: '{newCategory.Name}'");
                    break;
                }
            });
        }
    }

    #endregion
}