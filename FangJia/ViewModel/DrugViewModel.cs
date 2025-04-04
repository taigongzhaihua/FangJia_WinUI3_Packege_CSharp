﻿//------------------------------------------------------------------------
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
using Microsoft.UI.Xaml.Data;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TGZH.Pinyin;

namespace FangJia.ViewModel;

public partial class DrugViewModel : ObservableObject
{
    [ObservableProperty] public partial bool IsDrugSelected { get; set; }
    [ObservableProperty] public partial DrugSummary? SelectedDrugSummary { get; set; }
    [ObservableProperty] public partial Drug? SelectedDrug { get; set; }
    [ObservableProperty] public partial ObservableCollection<string>? SearchTexts { get; set; } = [];

    [ObservableProperty] public partial List<DrugSummary>? DrugSummaries { get; set; } = [];

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // 预计算的拼音和首字母索引
    private readonly Dictionary<DrugSummary, string> _pinyinCache = [];
    private readonly Dictionary<DrugSummary, string?> _initialCache = [];
    private bool _indexesBuilt;
    public CollectionViewSource DrugGroups { get; } = new() { IsSourceGrouped = true };

    public DrugViewModel()
    {
        _ = LoadDrugSummaryListAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                await BuildSearchIndexesAsync();
            }
            catch (Exception ex)
            {
                // 记录后台任务异常，避免崩溃和泄露
                Debug.WriteLine($"后台索引构建异常: {ex.Message}");
            }
        });
    }

    private async Task LoadDrugSummaryListAsync()
    {
        DrugSummaries?.Clear();
        await foreach (var ds in DrugManager.GetDrugSummaryListAsync())
        {
            DrugSummaries?.Add(ds);
        }

        UpdateDrugGroups();

    }

    private void UpdateDrugGroups()
    {
        // 分组
        var groupedSummaries =
            from ds in DrugSummaries

                // 根据 Category 分组
            group ds by ds.Category
            into g
            orderby g.Key

            // 创建 GroupInfoList 对象
            select new GroupInfoList(g.Key, g);

        // 更新 CollectionViewSource
        DrugGroups.Source = new ObservableCollection<GroupInfoList>(groupedSummaries);
    }

    /// <summary>
    /// 预计算所有药品名称的拼音和首字母，只需做一次
    /// </summary>
    private async Task BuildSearchIndexesAsync()
    {
        if (_indexesBuilt || DrugSummaries == null) return;

        try
        {
            Logger.Debug("开始构建药品拼音和首字母索引...");
            await UnifiedPinyinApi.InitializeAsync();

            var errorCount = 0;
            // 一次批量获取拼音和首字母，以提高性能
            var drugNameList = DrugSummaries.Select(ds => ds.Name).ToArray();
            var pinyinList = await UnifiedPinyinApi.GetWordsPinyinBatchAsync(drugNameList, PinyinFormat.WithoutTone);
            var initialList = await UnifiedPinyinApi.GetWordsPinyinBatchAsync(drugNameList, PinyinFormat.FirstLetter);
            // 将拼音列表添加到缓存字典
            foreach (var drug in DrugSummaries)
            {
                var pinyin = pinyinList[drug.Name!];
                var initials = initialList[drug.Name!];
                if (pinyin == null || initials == null)
                {
                    errorCount++;
                    continue;
                }
                _pinyinCache[drug] = pinyin.Replace(" ", "");
                _initialCache[drug] = initials.Replace(" ", "");
            }

            _indexesBuilt = true;
            Logger.Info("成功为 {DrugCount} 个药品建立拼音索引，处理失败: {ErrorCount} 个", _pinyinCache.Count, errorCount);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "建立药品索引失败: {ErrorMessage}", ex.Message);
        }
    }

    async partial void OnSelectedDrugSummaryChanged(DrugSummary? value)
    {
        try
        {
            SelectedDrug = value is not null ? await DrugManager.GetDrugAsync(value.Id) : null;
            if (SelectedDrug == null) return;
            SelectedDrug.DrugImage = await DrugManager.GetDrugImageAsync(SelectedDrug.Id);
            IsDrugSelected = true;
        }
        catch (Exception e)
        {
            Logger.Error($"获取药物错误: {e.Message}", e);
        }
    }

    /// <summary>
    /// 高效搜索药品，使用预计算的拼音索引
    /// </summary>
    public async Task SearchDrugSummariesAsync(string searchText)
    {
        var results = new List<string>();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            // 在UI线程清空结果
            await UpdateUiAsync(() => SearchTexts?.Clear());
            return;
        }

        if (DrugSummaries == null) return;

        // 如果索引尚未构建，先构建索引
        if (!_indexesBuilt)
        {
            await BuildSearchIndexesAsync();
        }

        // 统一为小写以忽略大小写
        var lowerSearchText = searchText.ToLower();

        // 使用预计算的索引进行高效搜索
        foreach (var drug in DrugSummaries)
        {
            if (drug.Name == null) continue;

            // 1. 直接匹配药品名称
            if (drug.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(drug.Name);
                continue;
            }

            // 2. 使用缓存的拼音匹配
            if (_pinyinCache.TryGetValue(drug, out var pinyin) &&
                pinyin.Contains(lowerSearchText, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(drug.Name);
                continue;
            }

            // 3. 使用缓存的首字母匹配
            if (!_initialCache.TryGetValue(drug, out var initials) ||
                !initials!.Contains(lowerSearchText, StringComparison.OrdinalIgnoreCase)) continue;
            results.Add(drug.Name);
        }

        // 更新UI
        await UpdateUiAsync(() =>
        {
            SearchTexts?.Clear();
            foreach (var result in results)
            {
                SearchTexts?.Add(result);
            }
        });
    }

    /// <summary>
    /// 在UI线程上执行操作
    /// </summary>
    private static async Task UpdateUiAsync(Action action)
    {
        await App.MainDispatcherQueue!.EnqueueAsync(action);
    }

    /// <summary>
    /// 原方法的兼容包装，但内部使用异步调用
    /// </summary>
    /// <param name="searchText">搜索文本</param>
    public void SearchDrugSummaries(string searchText)
    {
        // 使用Task.Run将异步方法封装在同步方法中，但这不是理想方案
        // 更好的做法是让调用者直接使用异步版本的方法
        _ = Task.Run(() => SearchDrugSummariesAsync(searchText));
    }

    public void ClearSearch()
    {
        SearchTexts?.Clear();
    }

    public void SelectDrug(string drugName)
    {
        SelectedDrugSummary = DrugSummaries?.FirstOrDefault(ds => ds.Name == drugName);
    }

    [RelayCommand]
    public async Task UpdateSelectedDrug(object key)
    {
        if (key is not string k) return;
        var value = SelectedDrug?.GetType().GetProperty(k)?.GetValue(SelectedDrug);
        if (value == null) return;
        var id = SelectedDrug!.Id;
        await DrugManager.UpdateDrugAsync(id, fields: (k, value.ToString()));
        if (k == "Category")
        {
            await LoadDrugSummaryListAsync();
            SelectedDrugSummary = DrugSummaries?.FirstOrDefault(ds => ds.Id == id);
        }
    }

    [RelayCommand]
    public async Task UpdateSelectedDrugImage(byte[] image)
    {
        if (SelectedDrug == null) return;
        var id = SelectedDrug.Id;
        await DrugManager.UpdateDrugImageAsync(id, image);
        SelectedDrug.DrugImage.Image = image;
    }

    [RelayCommand]
    public async Task DeleteSelectedDrug()
    {
        if (SelectedDrug == null) return;
        var id = SelectedDrug.Id;
        await DrugManager.DeleteDrugAsync(id);
        DrugSummaries?.Remove(DrugSummaries.FirstOrDefault(ds => ds.Id == id)!);
        UpdateDrugGroups();
        SelectedDrug = null;
        IsDrugSelected = false;
    }

    [RelayCommand]
    public async Task AddNewDrug()
    {
        var drug = new Drug
        {
            Category = "未分类",
            Name = "新药物",
            EnglishName = "New Drug",
            LatinName = "New Drug",
            Origin = "未知",
            Properties = "未知",
            Quality = "未知",
            Taste = "未知",
            Meridian = "未知",
            Effect = "未知",
            Notes = "未知",
            Processed = "未知",
            Source = "未知"
        };
        drug.Id = await DrugManager.InsertDrugAsync(drug);
        DrugSummaries?.Add(drug);
        UpdateDrugGroups();
        SelectedDrugSummary = DrugSummaries?.FirstOrDefault(ds => ds.Id == drug.Id);
        IsDrugSelected = true;
    }
}