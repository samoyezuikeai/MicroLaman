using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MicroLaman
{
    public partial class CameraShowForm : Form
    {
        private TucamOpen camera;
        private TucamFrame frame;
        private Thread captureThread;
        private volatile bool capturing;
        private bool apiInitialized;
        private bool bufferAllocated;
        private bool captureStarted;
        private bool drawInitialized;
        private IntPtr previewHandle;
        private int previewWidth;
        private int previewHeight;

        public CameraShowForm()
        {
            InitializeComponent();
        }

        private void CameraShowForm_Shown(object sender, EventArgs e)
        {
            previewWidth = previewPanel.ClientSize.Width;
            previewHeight = previewPanel.ClientSize.Height;
            previewHandle = previewPanel.Handle;
            StartCamera();
        }

        private void StartCamera()
        {
            IntPtr configPath = IntPtr.Zero;
            try
            {
                configPath = Marshal.StringToHGlobalAnsi(AppDomain.CurrentDomain.BaseDirectory);
                TucamInit init = new TucamInit { ConfigPath = configPath };
                EnsureSuccess(TUCamNative.TUCAM_Api_Init(ref init, 1000), "初始化 SDK");
                apiInitialized = true;

                if (init.CameraCount == 0)
                    throw new InvalidOperationException("未发现 TUCam 相机，请检查 MIchrome 20 的连接和驱动。\n设备管理器中出现相机并不代表 SDK 已能枚举到设备。");

                camera = new TucamOpen { Index = 0, CameraHandle = IntPtr.Zero };
                EnsureSuccess(TUCamNative.TUCAM_Dev_Open(ref camera), "打开第一个相机");

                frame = new TucamFrame
                {
                    Signature = new byte[8],
                    RequestedFormat = TUCamNative.UsualFrameFormat,
                    ReservedSize = 1,
                    Buffer = IntPtr.Zero
                };
                EnsureSuccess(TUCamNative.TUCAM_Buf_Alloc(camera.CameraHandle, ref frame), "分配图像缓冲区");
                bufferAllocated = true;

                EnsureSuccess(TUCamNative.TUCAM_Cap_Start(camera.CameraHandle, TUCamNative.SequenceCaptureMode), "开始连续采集");
                captureStarted = true;
                capturing = true;
                captureThread = new Thread(CaptureLoop)
                {
                    IsBackground = true,
                    Name = "TUCam capture"
                };
                captureThread.Start();
            }
            catch (DllNotFoundException)
            {
                ShowCameraError("找不到 TUCam.dll 或其依赖项。请确认已安装官方 TUCam SDK，并使用 x64 构建项目。");
                StopCamera();
            }
            catch (BadImageFormatException)
            {
                ShowCameraError("TUCam.dll 与程序位数不一致。当前项目应使用 x64 构建。");
                StopCamera();
            }
            catch (Exception ex)
            {
                ShowCameraError(ex.Message);
                StopCamera();
            }
            finally
            {
                if (configPath != IntPtr.Zero)
                    Marshal.FreeHGlobal(configPath);
            }
        }

        private void CaptureLoop()
        {
            IntPtr framePointer = IntPtr.Zero;
            try
            {
                framePointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TucamFrame)));
                while (capturing)
                {
                    TucamResult result = TUCamNative.TUCAM_Buf_WaitForFrame(camera.CameraHandle, ref frame, 1000);
                    if (result == TucamResult.Timeout)
                        continue;
                    if (result == TucamResult.Abort && !capturing)
                        break;
                    EnsureSuccess(result, "获取相机图像");

                    if (!drawInitialized)
                    {
                        TucamDrawInit drawInit = new TucamDrawInit
                        {
                            WindowHandle = previewHandle,
                            Mode = 0,
                            Channels = (sbyte)frame.Channels,
                            Width = frame.Width,
                            Height = frame.Height
                        };
                        EnsureSuccess(TUCamNative.TUCAM_Draw_Init(camera.CameraHandle, drawInit), "初始化图像显示");
                        drawInitialized = true;
                        HideStatusLabel();
                    }

                    Marshal.StructureToPtr(frame, framePointer, false);
                    TucamDraw draw = CreateDrawRectangle(framePointer, frame.Width, frame.Height);
                    EnsureSuccess(TUCamNative.TUCAM_Draw_Frame(camera.CameraHandle, ref draw), "显示相机图像");
                }
            }
            catch (Exception ex)
            {
                if (capturing)
                    ShowCameraError(ex.Message);
            }
            finally
            {
                if (framePointer != IntPtr.Zero)
                    Marshal.FreeHGlobal(framePointer);
            }
        }

        private TucamDraw CreateDrawRectangle(IntPtr framePointer, int imageWidth, int imageHeight)
        {
            int targetWidth = Math.Max(1, previewWidth);
            int targetHeight = Math.Max(1, previewHeight);
            double scale = Math.Min((double)targetWidth / imageWidth, (double)targetHeight / imageHeight);
            int width = Math.Max(4, ((int)(imageWidth * scale) / 4) * 4);
            int height = Math.Max(4, ((int)(imageHeight * scale) / 4) * 4);

            return new TucamDraw
            {
                SourceX = 0,
                SourceY = 0,
                SourceWidth = imageWidth,
                SourceHeight = imageHeight,
                DestinationX = (targetWidth - width) / 2,
                DestinationY = (targetHeight - height) / 2,
                DestinationWidth = width,
                DestinationHeight = height,
                Frame = framePointer
            };
        }

        private static void EnsureSuccess(TucamResult result, string operation)
        {
            if (result != TucamResult.Success)
                throw new InvalidOperationException(string.Format("{0}失败（TUCAM 返回码 0x{1:X8}）。", operation, (uint)result));
        }

        private void HideStatusLabel()
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            BeginInvoke((Action)(() => statusLabel.Visible = false));
        }

        private void ShowCameraError(string message)
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            BeginInvoke((Action)(() =>
            {
                statusLabel.Text = message;
                statusLabel.ForeColor = Color.OrangeRed;
                statusLabel.Visible = true;
                statusLabel.BringToFront();
            }));
        }

        private void PreviewPanel_Resize(object sender, EventArgs e)
        {
            previewWidth = previewPanel.ClientSize.Width;
            previewHeight = previewPanel.ClientSize.Height;
            previewPanel.Invalidate();
        }

        private void CameraShowForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopCamera();
        }

        private void StopCamera()
        {
            capturing = false;
            if (camera.CameraHandle != IntPtr.Zero && captureThread != null)
                TUCamNative.TUCAM_Buf_AbortWait(camera.CameraHandle);

            if (captureThread != null && captureThread != Thread.CurrentThread)
            {
                captureThread.Join();
                captureThread = null;
            }

            if (drawInitialized)
            {
                TUCamNative.TUCAM_Draw_Uninit(camera.CameraHandle);
                drawInitialized = false;
            }
            if (captureStarted)
            {
                TUCamNative.TUCAM_Cap_Stop(camera.CameraHandle);
                captureStarted = false;
            }
            if (bufferAllocated)
            {
                TUCamNative.TUCAM_Buf_Release(camera.CameraHandle);
                bufferAllocated = false;
            }
            if (camera.CameraHandle != IntPtr.Zero)
            {
                TUCamNative.TUCAM_Dev_Close(camera.CameraHandle);
                camera.CameraHandle = IntPtr.Zero;
            }
            if (apiInitialized)
            {
                TUCamNative.TUCAM_Api_Uninit();
                apiInitialized = false;
            }
        }
    }
}
