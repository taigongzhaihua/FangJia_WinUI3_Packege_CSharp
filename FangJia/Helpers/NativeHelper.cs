

//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------



using System.Runtime.InteropServices;

namespace FangJia.Helpers;

internal partial class NativeHelper
{
    public const int ErrorSuccess = 0;
    public const int ErrorInsufficientBuffer = 122;
    public const int AppmodelErrorNoPackage = 15700;

    [LibraryImport("api-ms-win-appmodel-runtime-l1-1-1", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U4)]
    internal static partial uint GetCurrentPackageId(ref int pBufferLength, out byte pBuffer);

    public static bool IsAppPackaged
    {
        get
        {
            var bufferSize = 0;
            var lastError = GetCurrentPackageId(ref bufferSize, out _);
            var isPackaged = lastError != AppmodelErrorNoPackage;

            return isPackaged;
        }
    }
}