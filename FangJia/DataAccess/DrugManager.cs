//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------
using System;
using System.IO;

namespace FangJia.DataAccess;

public class DrugManager
{
    private static readonly string DatabasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data.db");
    private readonly string _connectionString = $"Data Source={DatabasePath};";

}