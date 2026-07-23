using System;
using System.Runtime.InteropServices;

namespace MicroLaman
{
    internal enum TucamResult : uint
    {
        Success = 0x00000001,
        Abort = 0x80000207,
        Timeout = 0x80000208
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TucamInit
    {
        public uint CameraCount;
        public IntPtr ConfigPath;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TucamOpen
    {
        public uint Index;
        public IntPtr CameraHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TucamFrame
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Signature;
        public ushort HeaderSize;
        public ushort Offset;
        public ushort Width;
        public ushort Height;
        public uint WidthStep;
        public byte Depth;
        public byte Format;
        public byte Channels;
        public byte ElementBytes;
        public byte RequestedFormat;
        public uint Index;
        public uint ImageSize;
        public uint ReservedSize;
        public uint HistogramSize;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TucamDrawInit
    {
        public IntPtr WindowHandle;
        public int Mode;
        public sbyte Channels;
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TucamDraw
    {
        public int SourceX;
        public int SourceY;
        public int SourceWidth;
        public int SourceHeight;
        public int DestinationX;
        public int DestinationY;
        public int DestinationWidth;
        public int DestinationHeight;
        public IntPtr Frame;
    }

    internal static class TUCamNative
    {
        internal const byte UsualFrameFormat = 0x11;
        internal const uint SequenceCaptureMode = 0x00;
        internal const int ResolutionCapability = 0x00;
        internal const int AutoExposureCapability = 0x03;
        internal const int GlobalGainProperty = 0x00;
        internal const int ExposureTimeProperty = 0x01;
        internal const int DefaultDrawMode = 0x00;

        [DllImport("TUCam.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern TucamResult TUCAM_Api_Init(ref TucamInit init, int timeout);

        [DllImport("TUCam.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern TucamResult TUCAM_Api_Uninit();

        [DllImport("TUCam.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern TucamResult TUCAM_Dev_Open(ref TucamOpen open);

        [DllImport("TUCam.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern TucamResult TUCAM_Dev_Close(IntPtr cameraHandle);

        [DllImport("TUCam.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern TucamResult TUCAM_Capa_SetValue(IntPtr cameraHandle, int capabilityId, int value);

        [DllImport("TUCam.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern TucamResult TUCAM_Prop_GetValue(IntPtr cameraHandle, int propertyId, ref double value, int channel);

        [DllImport("TUCam.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern TucamResult TUCAM_Prop_SetValue(IntPtr cameraHandle, int propertyId, double value, int channel);

        [DllImport("TUCam.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern TucamResult TUCAM_Buf_Alloc(IntPtr cameraHandle, ref TucamFrame frame);

        [DllImport("TUCam.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern TucamResult TUCAM_Buf_Release(IntPtr cameraHandle);

        [DllImport("TUCam.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern TucamResult TUCAM_Buf_AbortWait(IntPtr cameraHandle);

        [DllImport("TUCam.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern TucamResult TUCAM_Buf_WaitForFrame(IntPtr cameraHandle, ref TucamFrame frame, int timeout);

        [DllImport("TUCam.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern TucamResult TUCAM_Cap_Start(IntPtr cameraHandle, uint mode);

        [DllImport("TUCam.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern TucamResult TUCAM_Cap_Stop(IntPtr cameraHandle);

        [DllImport("TUCam.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern TucamResult TUCAM_Draw_Init(IntPtr cameraHandle, TucamDrawInit drawInit);

        [DllImport("TUCam.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern TucamResult TUCAM_Draw_Frame(IntPtr cameraHandle, ref TucamDraw draw);

        [DllImport("TUCam.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern TucamResult TUCAM_Draw_Uninit(IntPtr cameraHandle);

        [DllImport("TUCam.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern TucamResult TUCAM_Vendor_SetQueueMode(
            IntPtr cameraHandle,
            [MarshalAs(UnmanagedType.Bool)] bool queueMode);
    }
}
