//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace FangJia.Helpers;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static class UiHelper
{
    public static IEnumerable<T> GetDescendantsOfType<T>(this DependencyObject start) where T : DependencyObject
    {
        return start.GetDescendants().OfType<T>();
    }

    public static IEnumerable<DependencyObject> GetDescendants(this DependencyObject start)
    {
        var queue = new Queue<DependencyObject>();
        var count1 = VisualTreeHelper.GetChildrenCount(start);

        for (var i = 0; i < count1; i++)
        {
            var child = VisualTreeHelper.GetChild(start, i);
            yield return child;
            queue.Enqueue(child);
        }

        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            var count2 = VisualTreeHelper.GetChildrenCount(parent);

            for (var i = 0; i < count2; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                yield return child;
                queue.Enqueue(child);
            }
        }
    }

    public static UIElement? FindElementByName(UIElement element, string name)
    {
        if (element.XamlRoot == null || element.XamlRoot.Content == null) return null;
        var ele = (element.XamlRoot.Content as FrameworkElement)?.FindName(name);
        return ele as UIElement;
    }

    // Confirmation of Action
    public static void AnnounceActionForAccessibility(UIElement? ue, string announcement, string activityId)
    {
        var peer = FrameworkElementAutomationPeer.FromElement(ue);
        peer.RaiseNotificationEvent(AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.ImportantMostRecent, announcement, activityId);
    }
}