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
using FangJia.Common;
using FangJia.DataAccess;
using Microsoft.UI.Xaml.Data;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FangJia.ViewModel;

public partial class DrugViewModel : ObservableObject
{
    [ObservableProperty] public partial bool IsDrugSelected { get; set; }
    [ObservableProperty] public partial DrugSummary? SelectedDrugSummary { get; set; }
    [ObservableProperty] public partial Drug? SelectedDrug { get; set; }
    [ObservableProperty] public partial ObservableCollection<string>? SearchTexts { get; set; } = [];

    private readonly List<DrugSummary>? _drugSummaries = [];
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public CollectionViewSource DrugGroups { get; } = new() { IsSourceGrouped = true };

    public DrugViewModel()
    {
        _ = LoadDrugSummaryListAsync();
    }

    private async Task LoadDrugSummaryListAsync()
    {
        _drugSummaries?.Clear();
        await foreach (var ds in DrugManager.GetDrugSummaryListAsync())
        {
            _drugSummaries?.Add(ds);
        }
        UpdateDrugGroups();
    }

    private void UpdateDrugGroups()
    {
        // 分组
        var groupedSummaries =
            from ds in _drugSummaries

                // 根据 Category 分组
            group ds by ds.Category into g
            orderby g.Key

            // 创建 GroupInfoList 对象
            select new GroupInfoList(g.Key, g);

        // 更新 CollectionViewSource
        DrugGroups.Source = new ObservableCollection<GroupInfoList>(groupedSummaries);
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
            _logger.Error($"获取药物错误: {e.Message}", e);
        }
    }

    public async Task SearchDrugSummaries(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText)) return;
        SearchTexts?.Clear();
        if (_drugSummaries == null) return;

        foreach (var drugSummary in _drugSummaries.Where(drugSummary => drugSummary.Name!.Contains(searchText)))
        {
            SearchTexts?.Add(drugSummary.Name!);
        }
    }

    [RelayCommand]
    public async Task UpdateSelectedDrug(object key)
    {
        if (key is not string k) return;
        var value = SelectedDrug?.GetType().GetProperty(k)?.GetValue(SelectedDrug);
        if (value == null) return;
        var id = SelectedDrug!.Id;
        await DrugManager.UpdateDrugAsync(id, CancellationToken.None, (k, value.ToString()));
        if (k == "Category")
        {
            await LoadDrugSummaryListAsync();
            SelectedDrugSummary = _drugSummaries?.FirstOrDefault(ds => ds.Id == id);
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
        _drugSummaries?.Remove(_drugSummaries.FirstOrDefault(ds => ds.Id == id)!);
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
        _drugSummaries?.Add(drug);
        UpdateDrugGroups();
        SelectedDrugSummary = _drugSummaries?.FirstOrDefault(ds => ds.Id == drug.Id);
        IsDrugSelected = true;
    }
}

