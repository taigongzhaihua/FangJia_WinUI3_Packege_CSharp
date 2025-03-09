//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------

using Autofac;
using System;

namespace FangJia.Helpers;

/// <summary>
/// 服务定位器类，用于管理和获取依赖注入容器中的服务。
/// </summary>
public static class Locator
{
    private static IContainer? _container;

    /// <summary>
    /// 初始化服务定位器，设置依赖注入容器。
    /// </summary>
    /// <param name="container">依赖注入容器实例。</param>
    public static void Initialize(IContainer container)
    {
        _container = container;
    }

    /// <summary>
    /// 获取指定类型的服务实例。
    /// </summary>
    /// <typeparam name="T">要获取的服务类型。</typeparam>
    /// <returns>指定类型的服务实例。</returns>
    public static T GetService<T>() where T : notnull
    {
        return (_container ?? throw new InvalidOperationException()).Resolve<T>();
    }

    /// <summary>
    /// 根据名称获取指定类型的服务实例。
    /// 若需要命名注册，请使用 Register 方法中的 Named 注册方式。
    /// </summary>
    /// <typeparam name="T">要获取的服务类型。</typeparam>
    /// <param name="name">服务的注册名称。</param>
    /// <returns>指定类型和名称的服务实例。</returns>
    public static T GetService<T>(string name) where T : notnull
    {
        return (_container ?? throw new InvalidOperationException()).ResolveNamed<T>(name);
    }
}