//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using Windows.Storage.Streams;

namespace FangJia.Converters;

public partial class ByteArrayToImgConverter : IValueConverter
{
    /// <summary>
    /// 将 byte[] 转换为 BitmapImage（异步设置图片源）
    /// </summary>
    /// <param name="value">输入的 byte[] 数据</param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="language"></param>
    /// <returns>转换后的 BitmapImage</returns>
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is byte[] { Length: > 0 } bytes)
        {
            // 创建 BitmapImage 并立即返回（图片源稍后设置）
            var bitmapImage = new BitmapImage();
            SetImageSourceAsync(bitmapImage, bytes);
            return bitmapImage;
        }

        return null;
    }

    /// <summary>
    /// 异步设置 BitmapImage 的 Source
    /// </summary>
    /// <param name="bitmapImage">要设置的 BitmapImage</param>
    /// <param name="bytes">图片数据</param>
    private static async void SetImageSourceAsync(BitmapImage? bitmapImage, byte[] bytes)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }

            stream.Seek(0);
            await bitmapImage?.SetSourceAsync(stream);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"设置图片源出错：{ex.Message}");
        }
    }

    /// <summary>
    /// 反向转换暂未实现
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

