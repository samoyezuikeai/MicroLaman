using System;
using System.Diagnostics;
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
        private System.Windows.Forms.Timer performanceTimer;
        private readonly Stopwatch frameRateWatch = new Stopwatch();
        private volatile bool capturing;
        private bool apiInitialized;
        private bool bufferAllocated;
        private bool captureStarted;
        private bool drawInitialized;
        private bool pixelPerfect = true;
        private IntPtr previewHandle;
        private int previewWidth;
        private int previewHeight;
        private int imageWidth;
        private int imageHeight;
        private int framesSinceUpdate;

        public CameraShowForm()
        {
            InitializeComponent();
            resolutionComboBox.SelectedIndex = 1;
            AutoExposureCheckBox_CheckedChanged(null, EventArgs.Empty);
        }

        private void CameraShowForm_Shown(object sender, EventArgs e)
        {
            previewWidth = previewPanel.ClientSize.Width;
            previewHeight = previewPanel.ClientSize.Height;
            previewHandle = previewPanel.Handle;

            performanceTimer = new System.Windows.Forms.Timer { Interval = 500 };
            performanceTimer.Tick += PerformanceTimer_Tick;
            performanceTimer.Start();
            StartCamera();
        }

        private void StartCamera()
        {
            IntPtr configPath = IntPtr.Zero;
            try
            {
                ShowCameraStatus("正在以低延迟对焦参数连接 MIchrome 20…", Color.Gainsboro);
                configPath = Marshal.StringToHGlobalAnsi(AppDomain.CurrentDomain.BaseDirectory);
                TucamInit init = new TucamInit { ConfigPath = configPath };
                EnsureSuccess(TUCamNative.TUCAM_Api_Init(ref init, 1000), "初始化 SDK");
                apiInitialized = true;

                if (init.CameraCount == 0)
                    throw new InvalidOperationException("未发现 TUCam 相机。请关闭官方软件或其他正在使用相机的 BoardControl 窗口后重试。");

                camera = new TucamOpen { Index = 0 };
                EnsureSuccess(TUCamNative.TUCAM_Dev_Open(ref camera), "打开第一个相机");

                ApplyFocusSettings();

                // 关闭队列模式，让 WaitForFrame 优先取得最新帧，避免绘制稍慢时累积延迟。
                TUCamNative.TUCAM_Vendor_SetQueueMode(camera.CameraHandle, false);

                frame = new TucamFrame
                {
                    Signature = new byte[8],
                    RequestedFormat = TUCamNative.UsualFrameFormat,
                    ReservedSize = 1
                };
                EnsureSuccess(TUCamNative.TUCAM_Buf_Alloc(camera.CameraHandle, ref frame), "分配图像缓冲区");
                bufferAllocated = true;

                EnsureSuccess(TUCamNative.TUCAM_Cap_Start(camera.CameraHandle, TUCamNative.SequenceCaptureMode), "开始连续采集");
                captureStarted = true;
                Interlocked.Exchange(ref framesSinceUpdate, 0);
                frameRateWatch.Restart();
                capturing = true;
                captureThread = new Thread(CaptureLoop)
                {
                    IsBackground = true,
                    Name = "TUCam low-latency capture"
                };
                captureThread.Start();
            }
            catch (DllNotFoundException)
            {
                ShowCameraError("找不到 TUCam.dll 或其依赖项。请确认官方 SDK 已安装且项目使用 x64 构建。");
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

        private void ApplyFocusSettings()
        {
            int resolution = Math.Max(0, resolutionComboBox.SelectedIndex);
            EnsureSuccess(
                TUCamNative.TUCAM_Capa_SetValue(camera.CameraHandle, TUCamNative.ResolutionCapability, resolution),
                "设置预览分辨率");

            if (autoExposureCheckBox.Checked)
            {
                // 与官方配套软件一致：由 SDK 根据当前显微镜照明持续调整亮度。
                EnsureSuccess(
                    TUCamNative.TUCAM_Capa_SetValue(camera.CameraHandle, TUCamNative.AutoExposureCapability, 1),
                    "开启自动曝光");
            }
            else
            {
                EnsureSuccess(
                    TUCamNative.TUCAM_Capa_SetValue(camera.CameraHandle, TUCamNative.AutoExposureCapability, 0),
                    "关闭自动曝光");
                EnsureSuccess(
                    TUCamNative.TUCAM_Prop_SetValue(camera.CameraHandle, TUCamNative.ExposureTimeProperty, (double)exposureNumeric.Value, 0),
                    "设置曝光时间");
                EnsureSuccess(
                    TUCamNative.TUCAM_Prop_SetValue(camera.CameraHandle, TUCamNative.GlobalGainProperty, (double)gainNumeric.Value, 0),
                    "设置全局增益");
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

                    imageWidth = frame.Width;
                    imageHeight = frame.Height;
                    Interlocked.Increment(ref framesSinceUpdate);

                    if (!drawInitialized)
                    {
                        InitializeNativeDrawing();
                        HideStatusLabel();
                    }

                    Marshal.StructureToPtr(frame, framePointer, false);
                    DrawFrameWithRecovery(framePointer);
                }
            }
            catch (Exception ex)
            {
                if (capturing)
                {
                    capturing = false;
                    ShowCameraError(ex.Message);
                }
            }
            finally
            {
                if (framePointer != IntPtr.Zero)
                    Marshal.FreeHGlobal(framePointer);
            }
        }

        private void InitializeNativeDrawing()
        {
            TucamDrawInit drawInit = new TucamDrawInit
            {
                WindowHandle = previewHandle,
                // 官方 WinForms 示例使用 TUDRAW_DFT。SDK 会自行选择兼容的绘制后端。
                Mode = TUCamNative.DefaultDrawMode,
                Channels = (sbyte)frame.Channels,
                Width = frame.Width,
                Height = frame.Height
            };
            EnsureSuccess(TUCamNative.TUCAM_Draw_Init(camera.CameraHandle, drawInit), "初始化图像显示");
            drawInitialized = true;
        }

        private void DrawFrameWithRecovery(IntPtr framePointer)
        {
            TucamDraw draw = CreateDrawRectangle(framePointer, frame.Width, frame.Height);
            TucamResult result = TUCamNative.TUCAM_Draw_Frame(camera.CameraHandle, ref draw);
            if (result == TucamResult.Success)
                return;

            // 某些驱动/显卡组合不支持原生绘制器的源区域裁剪。采集仍然正常，
            // 此时退回官方示例使用的完整源图绘制，避免将绘制错误当成相机错误。
            if (pixelPerfect)
            {
                pixelPerfect = false;
                draw = CreateDrawRectangle(framePointer, frame.Width, frame.Height);
                result = TUCamNative.TUCAM_Draw_Frame(camera.CameraHandle, ref draw);
                if (result == TucamResult.Success)
                {
                    RunOnUiThread(() => zoomButton.Text = "1:1 对焦");
                    return;
                }
            }

            EnsureSuccess(result, "显示相机图像");
        }

        private TucamDraw CreateDrawRectangle(IntPtr framePointer, int frameWidth, int frameHeight)
        {
            int targetWidth = Math.Max(4, previewWidth);
            int targetHeight = Math.Max(4, previewHeight);

            if (pixelPerfect)
            {
                int sourceWidth = AlignToFour(Math.Min(frameWidth, targetWidth));
                int sourceHeight = AlignToFour(Math.Min(frameHeight, targetHeight));
                int sourceX = AlignToFour((frameWidth - sourceWidth) / 2);
                int sourceY = AlignToFour((frameHeight - sourceHeight) / 2);

                return new TucamDraw
                {
                    SourceX = sourceX,
                    SourceY = sourceY,
                    SourceWidth = sourceWidth,
                    SourceHeight = sourceHeight,
                    DestinationX = (targetWidth - sourceWidth) / 2,
                    DestinationY = (targetHeight - sourceHeight) / 2,
                    DestinationWidth = sourceWidth,
                    DestinationHeight = sourceHeight,
                    Frame = framePointer
                };
            }

            double scale = Math.Min((double)targetWidth / frameWidth, (double)targetHeight / frameHeight);
            int width = AlignToFour((int)(frameWidth * scale));
            int height = AlignToFour((int)(frameHeight * scale));
            return new TucamDraw
            {
                SourceX = 0,
                SourceY = 0,
                SourceWidth = frameWidth,
                SourceHeight = frameHeight,
                DestinationX = (targetWidth - width) / 2,
                DestinationY = (targetHeight - height) / 2,
                DestinationWidth = width,
                DestinationHeight = height,
                Frame = framePointer
            };
        }

        private static int AlignToFour(int value)
        {
            return Math.Max(4, (value / 4) * 4);
        }

        private static void EnsureSuccess(TucamResult result, string operation)
        {
            if (result != TucamResult.Success)
                throw new InvalidOperationException(string.Format("{0}失败（TUCAM 返回码 0x{1:X8}）。", operation, (uint)result));
        }

        private void PerformanceTimer_Tick(object sender, EventArgs e)
        {
            long elapsed = Math.Max(1, frameRateWatch.ElapsedMilliseconds);
            int frames = Interlocked.Exchange(ref framesSinceUpdate, 0);
            double fps = frames * 1000.0 / elapsed;
            frameRateWatch.Restart();
            string mode = pixelPerfect ? "1:1 对焦" : "适应窗口";
            performanceLabel.Text = string.Format(
                "{0:F1} FPS | {1}×{2} | {3}",
                fps,
                imageWidth,
                imageHeight,
                mode);

            if (autoExposureCheckBox.Checked && camera.CameraHandle != IntPtr.Zero)
                UpdateAutomaticExposureValues();
        }

        private void UpdateAutomaticExposureValues()
        {
            double exposure = 0;
            double gain = 0;
            if (TUCamNative.TUCAM_Prop_GetValue(camera.CameraHandle, TUCamNative.ExposureTimeProperty, ref exposure, 0) == TucamResult.Success)
                exposureNumeric.Value = ClampDecimal((decimal)exposure, exposureNumeric.Minimum, exposureNumeric.Maximum);
            if (TUCamNative.TUCAM_Prop_GetValue(camera.CameraHandle, TUCamNative.GlobalGainProperty, ref gain, 0) == TucamResult.Success)
                gainNumeric.Value = ClampDecimal((decimal)gain, gainNumeric.Minimum, gainNumeric.Maximum);
        }

        private static decimal ClampDecimal(decimal value, decimal minimum, decimal maximum)
        {
            return Math.Min(maximum, Math.Max(minimum, value));
        }

        private void AutoExposureCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            bool manual = !autoExposureCheckBox.Checked;
            exposureNumeric.Enabled = manual;
            gainNumeric.Enabled = manual;
        }

        private void ApplySettingsButton_Click(object sender, EventArgs e)
        {
            applySettingsButton.Enabled = false;
            try
            {
                StopCamera();
                StartCamera();
            }
            finally
            {
                applySettingsButton.Enabled = true;
            }
        }

        private void ZoomButton_Click(object sender, EventArgs e)
        {
            ToggleZoomMode();
        }

        private void PreviewPanel_DoubleClick(object sender, EventArgs e)
        {
            ToggleZoomMode();
        }

        private void ToggleZoomMode()
        {
            pixelPerfect = !pixelPerfect;
            zoomButton.Text = pixelPerfect ? "适应窗口" : "1:1 对焦";
            previewPanel.Invalidate();
        }

        private void HideStatusLabel()
        {
            RunOnUiThread(() => statusLabel.Visible = false);
        }

        private void ShowCameraStatus(string message, Color color)
        {
            RunOnUiThread(() =>
            {
                statusLabel.Text = message;
                statusLabel.ForeColor = color;
                statusLabel.Visible = true;
                statusLabel.BringToFront();
            });
        }

        private void ShowCameraError(string message)
        {
            ShowCameraStatus(message, Color.OrangeRed);
        }

        private void RunOnUiThread(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            try
            {
                if (InvokeRequired)
                    BeginInvoke(action);
                else
                    action();
            }
            catch (InvalidOperationException)
            {
                // 窗口正在关闭时忽略迟到的状态更新。
            }
        }

        private void PreviewPanel_Resize(object sender, EventArgs e)
        {
            previewWidth = previewPanel.ClientSize.Width;
            previewHeight = previewPanel.ClientSize.Height;
            previewPanel.Invalidate();
        }

        private void CameraShowForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (performanceTimer != null)
            {
                performanceTimer.Stop();
                performanceTimer.Tick -= PerformanceTimer_Tick;
                performanceTimer.Dispose();
                performanceTimer = null;
            }
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

            imageWidth = 0;
            imageHeight = 0;
            Interlocked.Exchange(ref framesSinceUpdate, 0);
        }
    }
}
