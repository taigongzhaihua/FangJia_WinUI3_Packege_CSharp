//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System.Runtime.InteropServices;

namespace FangJia.Helpers
{
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
}