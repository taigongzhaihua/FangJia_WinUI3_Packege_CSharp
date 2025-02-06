using Unity;

namespace FangJia.Helpers;

/// <summary>
/// 服务定位器类，用于管理和获取依赖注入容器中的服务。
/// </summary>
public static class Locator
{
    private static IUnityContainer? _container;

    /// <summary>
    /// 初始化服务定位器，设置依赖注入容器。
    /// </summary>
    /// <param name="container">依赖注入容器实例。</param>
    public static void Initialize(IUnityContainer? container)
    {
        _container = container;
    }

    /// <summary>
    /// 获取指定类型的服务实例。
    /// </summary>
    /// <typeparam name="T">要获取的服务类型。</typeparam>
    /// <returns>指定类型的服务实例。</returns>
    public static T GetService<T>()
    {
        return _container.Resolve<T>();
    }

    /// <summary>
    /// 根据名称获取指定类型的服务实例。
    /// </summary>
    /// <typeparam name="T">要获取的服务类型。</typeparam>
    /// <param name="name">服务的注册名称。</param>
    /// <returns>指定类型和名称的服务实例。</returns>
    public static T GetService<T>(string name)
    {
        return _container.Resolve<T>(name);
    }
}