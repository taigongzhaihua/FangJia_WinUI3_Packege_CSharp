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
    internal class NativeHelper
    {
        public const int ErrorSuccess = 0;
        public const int ErrorInsufficientBuffer = 122;
        public const int AppmodelErrorNoPackage = 15700;

        [DllImport("api-ms-win-appmodel-runtime-l1-1-1", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U4)]
#pragma warning disable SYSLIB1054
        internal static extern uint GetCurrentPackageId(ref int pBufferLength, out byte pBuffer);
#pragma warning restore SYSLIB1054

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