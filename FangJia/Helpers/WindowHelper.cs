﻿//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using WinRT.Interop;

namespace FangJia.Helpers
{
    // NavigationHelper class to allow the app to find the Window that contains an
    // arbitrary UIElement (GetWindowForElement).  To do this, we keep track
    // of all active Windows.  The app code must call WindowHelper.CreateWindow
    // rather than "new Window" so we can keep track of all the relevant
    // windows.  In the future, we would like to support this in platform APIs.
    public class WindowHelper
    {
        public static Window? CreateWindow()
        {
            var newWindow = new Window
            {
                SystemBackdrop = new MicaBackdrop()
            };
            TrackWindow(newWindow);
            return newWindow;
        }

        public static void TrackWindow(Window? window)
        {
            if (window == null) return;
            window.Closed += (_, _) => { ActiveWindows.Remove(window); };
            ActiveWindows.Add(window);
        }

        public static AppWindow GetAppWindow(Window window)
        {
            var hWnd = WindowNative.GetWindowHandle(window);
            var wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(wndId);
        }

        public static Window? GetWindowForElement(UIElement element)
        {
            return element.XamlRoot != null ? ActiveWindows.FirstOrDefault(window => element.XamlRoot == window?.Content.XamlRoot) : null;
        }
        // get dpi for an element
        public static double GetRasterizationScaleForElement(UIElement element)
        {
            if (element.XamlRoot == null) return 0.0;
            return ActiveWindows.Any(window => element.XamlRoot == window?.Content.XamlRoot) ? element.XamlRoot.RasterizationScale : 0.0;
        }

        public static List<Window?> ActiveWindows { get; } = [];

        public static StorageFolder GetAppLocalFolder()
        {
            var localFolder = !NativeHelper.IsAppPackaged ? Task.Run(async () => await StorageFolder.GetFolderFromPathAsync(System.AppContext.BaseDirectory)).Result : ApplicationData.Current.LocalFolder;
            return localFolder;
        }
    }
}
