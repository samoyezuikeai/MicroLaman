using System;
using System.Collections.Generic;
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
        private readonly object frameArrivalSync = new object();
        private readonly object snapshotCaptureSync = new object();
        private readonly object snapshotStateSync = new object();
        private readonly object liveTrackingSync = new object();
        private readonly object overlayModelSync = new object();
        private readonly AutoResetEvent snapshotReady = new AutoResetEvent(false);
        private readonly object previewDrawSync = new object();
        private RectangleSelectionOverlay selectionOverlay;
        private volatile bool capturing;
        private bool apiInitialized;
        private bool bufferAllocated;
        private bool captureStarted;
        private bool drawInitialized;
        private IntPtr previewHandle;
        private int previewWidth;
        private int previewHeight;
        private int imageWidth;
        private int imageHeight;
        private int framesSinceUpdate;
        private bool rectangleToolEnabled;
        private bool drawingRectangle;
        private Point rectangleStart;
        private RectangleF selectionImageRegion = RectangleF.Empty;
        private RectangleF displayedSelectionImageRegion = RectangleF.Empty;
        private long capturedFrameSequence;
        private bool snapshotRequested;
        private int snapshotRequestId;
        private int snapshotFramesToSkip;
        private int snapshotMaximumDimension = 768;
        private GrayFrameSnapshot snapshotResult;
        private Exception snapshotException;
        private byte[] snapshotRawBuffer;
        private PreparedImageRegistration liveTrackingRegistration;
        private bool liveTrackingEnabled;
        private bool stageTrackingEnabled;
        private StagePosition trackingOrigin;
        private StagePixelCalibration trackingCalibration;
        private readonly List<PointF> recordedScanPointsImage = new List<PointF>();

        internal int CameraImageWidth { get { return imageWidth; } }
        internal int CameraImageHeight { get { return imageHeight; } }

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
            selectionOverlay = new RectangleSelectionOverlay();
            UpdateOverlayGridSize();

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
                    Interlocked.Increment(ref capturedFrameSequence);
                    lock (frameArrivalSync)
                        Monitor.PulseAll(frameArrivalSync);
                    FulfillSnapshotRequest();
                    UpdateLiveOverlayTracking();

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
            TucamResult result;
            lock (previewDrawSync)
            {
                result = TUCamNative.TUCAM_Draw_Frame(camera.CameraHandle, ref draw);
                RectangleSelectionOverlay overlay = selectionOverlay;
                if (result == TucamResult.Success && overlay != null && previewHandle != IntPtr.Zero)
                {
                    using (Graphics graphics = Graphics.FromHwnd(previewHandle))
                        overlay.Draw(graphics, new Size(previewWidth, previewHeight));
                }
            }
            if (result == TucamResult.Success)
                return;

            EnsureSuccess(result, "显示相机图像");
        }

        private TucamDraw CreateDrawRectangle(IntPtr framePointer, int frameWidth, int frameHeight)
        {
            Rectangle source;
            Rectangle destination;
            GetCameraViewRectangles(frameWidth, frameHeight, out source, out destination);
            return new TucamDraw
            {
                SourceX = source.X,
                SourceY = source.Y,
                SourceWidth = source.Width,
                SourceHeight = source.Height,
                DestinationX = destination.X,
                DestinationY = destination.Y,
                DestinationWidth = destination.Width,
                DestinationHeight = destination.Height,
                Frame = framePointer
            };
        }

        private void GetCameraViewRectangles(int frameWidth, int frameHeight, out Rectangle source, out Rectangle destination)
        {
            int targetWidth = Math.Max(4, previewWidth);
            int targetHeight = Math.Max(4, previewHeight);

            double scale = Math.Min((double)targetWidth / frameWidth, (double)targetHeight / frameHeight);
            int width = AlignToFour((int)(frameWidth * scale));
            int height = AlignToFour((int)(frameHeight * scale));
            source = new Rectangle(0, 0, frameWidth, frameHeight);
            destination = new Rectangle(
                (targetWidth - width) / 2,
                (targetHeight - height) / 2,
                width,
                height);
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
            performanceLabel.Text = string.Format(
                "{0:F1} FPS | {1}×{2}",
                fps,
                imageWidth,
                imageHeight);

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

        private void RectangleToolButton_Click(object sender, EventArgs e)
        {
            rectangleToolEnabled = !rectangleToolEnabled;
            if (!rectangleToolEnabled)
                CancelRectangleDrawing();

            previewPanel.Cursor = rectangleToolEnabled ? Cursors.Cross : Cursors.Default;
            rectangleToolButton.BackColor = rectangleToolEnabled
                ? Color.FromArgb(55, 95, 115)
                : controlPanel.BackColor;
            rectangleToolButton.FlatAppearance.BorderColor = rectangleToolEnabled
                ? Color.DeepSkyBlue
                : Color.DimGray;
            rectangleToolButton.Invalidate();
        }

        private void RectangleToolButton_Paint(object sender, PaintEventArgs e)
        {
            Color color = rectangleToolEnabled ? Color.DeepSkyBlue : Color.DarkGray;
            using (Pen pen = new Pen(color, 1.6f))
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.DrawLine(pen, 10, 7, 22, 7);
                e.Graphics.DrawLine(pen, 10, 7, 10, 18);
                e.Graphics.DrawLine(pen, 16, 5, 16, 20);
                e.Graphics.DrawLine(pen, 16, 20, 28, 20);
                e.Graphics.DrawLine(pen, 28, 13, 28, 22);
            }
        }

        private void ScanPointCount_ValueChanged(object sender, EventArgs e)
        {
            UpdateOverlayGridSize();
        }

        private void UpdateOverlayGridSize()
        {
            if (selectionOverlay == null)
                return;

            selectionOverlay.SetGridSize((int)xPointCountNumeric.Value, (int)yPointCountNumeric.Value);
        }

        private void PreviewPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (!rectangleToolEnabled || e.Button != MouseButtons.Left)
                return;

            Rectangle source;
            Rectangle destination;
            if (!TryGetCameraViewRectangles(out source, out destination) || !destination.Contains(e.Location))
                return;

            CancelRectangleDrawing();
            stageTrackingEnabled = false;
            recordedScanPointsImage.Clear();
            selectionImageRegion = RectangleF.Empty;
            displayedSelectionImageRegion = RectangleF.Empty;
            if (selectionOverlay != null)
                selectionOverlay.ClearSelection();
            rectangleStart = ClampToRectangle(e.Location, destination);
            drawingRectangle = true;
            previewPanel.Capture = true;
        }

        private void PreviewPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (!drawingRectangle)
                return;

            Rectangle source;
            Rectangle destination;
            if (!TryGetCameraViewRectangles(out source, out destination))
                return;

            Rectangle rectangle = NormalizeRectangle(rectangleStart, ClampToRectangle(e.Location, destination));
            ShowOverlayClientRectangle(rectangle);
        }

        private void PreviewPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (!drawingRectangle || e.Button != MouseButtons.Left)
                return;

            Rectangle source;
            Rectangle destination;
            if (!TryGetCameraViewRectangles(out source, out destination))
            {
                CancelRectangleDrawing();
                return;
            }

            Rectangle completed = NormalizeRectangle(rectangleStart, ClampToRectangle(e.Location, destination));
            drawingRectangle = false;
            previewPanel.Capture = false;
            if (completed.Width > 3 && completed.Height > 3)
            {
                selectionImageRegion = ClientToImageRegion(completed, source, destination);
                displayedSelectionImageRegion = selectionImageRegion;
                UpdateSelectionOverlayFromImageCoordinates();
            }
            else
            {
                selectionImageRegion = RectangleF.Empty;
                displayedSelectionImageRegion = RectangleF.Empty;
                if (selectionOverlay != null)
                    selectionOverlay.ClearSelection();
            }
        }

        private void CancelRectangleDrawing()
        {
            if (!drawingRectangle)
                return;

            drawingRectangle = false;
            previewPanel.Capture = false;
            selectionImageRegion = RectangleF.Empty;
            displayedSelectionImageRegion = RectangleF.Empty;
            if (selectionOverlay != null)
                selectionOverlay.ClearSelection();
        }

        private void ShowOverlayClientRectangle(Rectangle rectangle)
        {
            int clientWidth = previewWidth;
            int clientHeight = previewHeight;
            if (selectionOverlay == null || clientWidth <= 0 || clientHeight <= 0)
                return;

            if (rectangle.IsEmpty)
            {
                selectionOverlay.ClearSelection();
                return;
            }

            selectionOverlay.SetSelection(new RectangleF(
                (float)rectangle.X / clientWidth,
                (float)rectangle.Y / clientHeight,
                (float)rectangle.Width / clientWidth,
                (float)rectangle.Height / clientHeight));
        }

        private bool TryGetCameraViewRectangles(out Rectangle source, out Rectangle destination)
        {
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                source = Rectangle.Empty;
                destination = Rectangle.Empty;
                return false;
            }

            GetCameraViewRectangles(imageWidth, imageHeight, out source, out destination);
            return source.Width > 0 && source.Height > 0 && destination.Width > 0 && destination.Height > 0;
        }

        private RectangleF ClientToImageRegion(Rectangle rectangle, Rectangle source, Rectangle destination)
        {
            float left = source.Left + (rectangle.Left - destination.Left) * (float)source.Width / destination.Width;
            float top = source.Top + (rectangle.Top - destination.Top) * (float)source.Height / destination.Height;
            float right = source.Left + (rectangle.Right - destination.Left) * (float)source.Width / destination.Width;
            float bottom = source.Top + (rectangle.Bottom - destination.Top) * (float)source.Height / destination.Height;
            return RectangleF.FromLTRB(
                left / imageWidth,
                top / imageHeight,
                right / imageWidth,
                bottom / imageHeight);
        }

        private void UpdateSelectionOverlayFromImageCoordinates()
        {
            lock (overlayModelSync)
            {
                if (selectionOverlay == null || drawingRectangle)
                    return;

                if (displayedSelectionImageRegion.IsEmpty)
                {
                    selectionOverlay.ClearSelection();
                    return;
                }

                Rectangle source;
                Rectangle destination;
                if (!TryGetCameraViewRectangles(out source, out destination))
                    return;

                float imageLeft = displayedSelectionImageRegion.Left * imageWidth;
                float imageTop = displayedSelectionImageRegion.Top * imageHeight;
                float imageRight = displayedSelectionImageRegion.Right * imageWidth;
                float imageBottom = displayedSelectionImageRegion.Bottom * imageHeight;
                Rectangle clientRectangle = Rectangle.FromLTRB(
                    (int)Math.Round(destination.Left + (imageLeft - source.Left) * destination.Width / source.Width),
                    (int)Math.Round(destination.Top + (imageTop - source.Top) * destination.Height / source.Height),
                    (int)Math.Round(destination.Left + (imageRight - source.Left) * destination.Width / source.Width),
                    (int)Math.Round(destination.Top + (imageBottom - source.Top) * destination.Height / source.Height));

                clientRectangle = Rectangle.Intersect(clientRectangle, destination);
                ShowOverlayClientRectangle(clientRectangle);
                UpdateRecordedScanPointsOverlay(source, destination);
            }
        }

        private void UpdateRecordedScanPointsOverlay(Rectangle source, Rectangle destination)
        {
            int clientWidth = previewWidth;
            int clientHeight = previewHeight;
            if (selectionOverlay == null || clientWidth <= 0 || clientHeight <= 0)
                return;

            float offsetX = displayedSelectionImageRegion.X - selectionImageRegion.X;
            float offsetY = displayedSelectionImageRegion.Y - selectionImageRegion.Y;
            List<PointF> clientPoints = new List<PointF>(recordedScanPointsImage.Count);
            foreach (PointF point in recordedScanPointsImage)
            {
                float imageX = (point.X + offsetX) * imageWidth;
                float imageY = (point.Y + offsetY) * imageHeight;
                float clientX = destination.Left + (imageX - source.Left) * destination.Width / source.Width;
                float clientY = destination.Top + (imageY - source.Top) * destination.Height / source.Height;
                clientPoints.Add(new PointF(
                    clientX / clientWidth,
                    clientY / clientHeight));
            }
            selectionOverlay.SetRecordedScanPoints(clientPoints);
        }

        private static Point ClampToRectangle(Point point, Rectangle rectangle)
        {
            return new Point(
                Math.Max(rectangle.Left, Math.Min(rectangle.Right, point.X)),
                Math.Max(rectangle.Top, Math.Min(rectangle.Bottom, point.Y)));
        }

        private static Rectangle NormalizeRectangle(Point start, Point end)
        {
            return Rectangle.FromLTRB(
                Math.Min(start.X, end.X),
                Math.Min(start.Y, end.Y),
                Math.Max(start.X, end.X),
                Math.Max(start.Y, end.Y));
        }

        public bool TryGetSnakeScanPoints(out List<PointF> normalizedImagePoints, out string errorMessage)
        {
            normalizedImagePoints = new List<PointF>();
            errorMessage = null;

            if (selectionImageRegion.IsEmpty)
            {
                errorMessage = "请先在相机窗口中框选扫描区域。";
                return false;
            }

            int xPointCount = (int)xPointCountNumeric.Value;
            int yPointCount = (int)yPointCountNumeric.Value;
            for (int row = 0; row < yPointCount; row++)
            {
                for (int step = 0; step < xPointCount; step++)
                {
                    int column = row % 2 == 0 ? step : xPointCount - 1 - step;
                    float x = xPointCount == 1
                        ? selectionImageRegion.Left + selectionImageRegion.Width / 2f
                        : selectionImageRegion.Left + column * selectionImageRegion.Width / (xPointCount - 1);
                    float y = yPointCount == 1
                        ? selectionImageRegion.Top + selectionImageRegion.Height / 2f
                        : selectionImageRegion.Top + row * selectionImageRegion.Height / (yPointCount - 1);
                    normalizedImagePoints.Add(new PointF(x, y));
                }
            }

            return true;
        }

        internal GrayFrameSnapshot CaptureGrayFrame(int framesToSkip, int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            return CaptureGrayFrame(framesToSkip, timeoutMilliseconds, cancellationToken, 768);
        }

        internal GrayFrameSnapshot CaptureGrayFrame(
            int framesToSkip,
            int timeoutMilliseconds,
            CancellationToken cancellationToken,
            int maximumDimension)
        {
            lock (snapshotCaptureSync)
            {
                snapshotReady.Reset();
                lock (snapshotStateSync)
                {
                    if (!capturing)
                        throw new InvalidOperationException("相机未开始采集，无法执行图像标定。");
                    snapshotResult = null;
                    snapshotException = null;
                    snapshotFramesToSkip = Math.Max(0, framesToSkip);
                    snapshotMaximumDimension = Math.Max(128, maximumDimension);
                    snapshotRequestId++;
                    snapshotRequested = true;
                }

                int waitResult = WaitHandle.WaitAny(
                    new WaitHandle[] { snapshotReady, cancellationToken.WaitHandle },
                    timeoutMilliseconds);
                if (waitResult == 1)
                {
                    CancelSnapshotRequest();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                if (waitResult == WaitHandle.WaitTimeout)
                {
                    CancelSnapshotRequest();
                    throw new TimeoutException("等待相机校准帧超时。");
                }

                lock (snapshotStateSync)
                {
                    if (snapshotException != null)
                        throw new InvalidOperationException("读取相机校准帧失败。", snapshotException);
                    if (snapshotResult == null)
                        throw new InvalidOperationException("没有取得有效的相机校准帧。");
                    return snapshotResult;
                }
            }
        }

        internal void WaitForFreshFrames(int count, int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            long target = Interlocked.Read(ref capturedFrameSequence) + Math.Max(1, count);
            Stopwatch timeout = Stopwatch.StartNew();
            lock (frameArrivalSync)
            {
                while (Interlocked.Read(ref capturedFrameSequence) < target)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int remaining = timeoutMilliseconds - (int)timeout.ElapsedMilliseconds;
                    if (remaining <= 0 || !Monitor.Wait(frameArrivalSync, Math.Min(remaining, 200)))
                    {
                        if (timeout.ElapsedMilliseconds >= timeoutMilliseconds)
                            throw new TimeoutException("等待相机刷新画面超时。");
                    }
                }
            }
        }

        internal void SetTemporaryOverlayPixelOffset(float pixelX, float pixelY)
        {
            RunOnUiThread(() => ApplyTemporaryOverlayPixelOffset(pixelX, pixelY));
        }

        private void ApplyTemporaryOverlayPixelOffset(float pixelX, float pixelY)
        {
            lock (overlayModelSync)
            {
                if (selectionImageRegion.IsEmpty || imageWidth <= 0 || imageHeight <= 0)
                    return;
                displayedSelectionImageRegion = new RectangleF(
                    selectionImageRegion.X + pixelX / imageWidth,
                    selectionImageRegion.Y + pixelY / imageHeight,
                    selectionImageRegion.Width,
                    selectionImageRegion.Height);
                UpdateSelectionOverlayFromImageCoordinates();
            }
        }

        internal void PrepareForNewScan()
        {
            RunOnUiThread(() =>
            {
                stageTrackingEnabled = false;
                trackingCalibration = null;
                lock (liveTrackingSync)
                {
                    liveTrackingEnabled = false;
                    liveTrackingRegistration = null;
                }
                displayedSelectionImageRegion = selectionImageRegion;
                recordedScanPointsImage.Clear();
                if (selectionOverlay != null)
                    selectionOverlay.SetRecordedScanPoints(null);
                UpdateSelectionOverlayFromImageCoordinates();
            });
        }

        internal void RecordScanVisitAtViewCenter(float imageShiftX, float imageShiftY)
        {
            RunOnUiThread(() =>
            {
                if (imageWidth <= 0 || imageHeight <= 0)
                    return;

                // The current image is the original specimen image translated by imageShift.
                // Therefore the specimen point now at the view center was originally here.
                recordedScanPointsImage.Add(new PointF(
                    0.5f - imageShiftX / imageWidth,
                    0.5f - imageShiftY / imageHeight));
                UpdateSelectionOverlayFromImageCoordinates();
            });
        }

        internal void BeginStageTracking(StagePosition origin, StagePixelCalibration calibration)
        {
            RunOnUiThread(() =>
            {
                trackingOrigin = origin;
                trackingCalibration = calibration;
                stageTrackingEnabled = true;
                displayedSelectionImageRegion = selectionImageRegion;
                UpdateSelectionOverlayFromImageCoordinates();
            });
        }

        internal void BeginLiveOverlayTracking(GrayFrameSnapshot originFrame)
        {
            PreparedImageRegistration registration = ImageRegistration.Prepare(originFrame);
            lock (liveTrackingSync)
            {
                liveTrackingRegistration = registration;
                liveTrackingEnabled = true;
            }
        }

        private void UpdateLiveOverlayTracking()
        {
            PreparedImageRegistration registration;
            lock (liveTrackingSync)
            {
                if (!liveTrackingEnabled || liveTrackingRegistration == null)
                    return;
                registration = liveTrackingRegistration;
            }

            try
            {
                GrayFrameSnapshot current = CreateGrayFrameSnapshot(frame, 384);
                ImageTranslation translation;
                lock (liveTrackingSync)
                {
                    if (!liveTrackingEnabled || !ReferenceEquals(registration, liveTrackingRegistration))
                        return;
                    translation = ImageRegistration.MeasureTranslation(registration, current);
                }
                if (translation.Confidence < 4)
                    return;

                ApplyTemporaryOverlayPixelOffset(
                    (float)translation.X,
                    (float)translation.Y);
            }
            catch (InvalidOperationException)
            {
                // A single low-quality frame must not stop camera acquisition.
            }
        }

        internal void UpdateTrackedStagePosition(StagePosition position)
        {
            RunOnUiThread(() =>
            {
                if (!stageTrackingEnabled || trackingCalibration == null || selectionImageRegion.IsEmpty)
                    return;
                PointF shift = trackingCalibration.StageDeltaToImage(
                    position.X - trackingOrigin.X,
                    position.Y - trackingOrigin.Y);
                displayedSelectionImageRegion = new RectangleF(
                    selectionImageRegion.X + shift.X / imageWidth,
                    selectionImageRegion.Y + shift.Y / imageHeight,
                    selectionImageRegion.Width,
                    selectionImageRegion.Height);
                UpdateSelectionOverlayFromImageCoordinates();
            });
        }

        private void FulfillSnapshotRequest()
        {
            int requestId;
            lock (snapshotStateSync)
            {
                if (!snapshotRequested)
                    return;
                if (snapshotFramesToSkip > 0)
                {
                    snapshotFramesToSkip--;
                    return;
                }
                requestId = snapshotRequestId;
                snapshotRequested = false;
            }

            GrayFrameSnapshot result = null;
            Exception error = null;
            try
            {
                result = CreateGrayFrameSnapshot(frame, snapshotMaximumDimension);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            lock (snapshotStateSync)
            {
                if (requestId != snapshotRequestId)
                    return;
                snapshotResult = result;
                snapshotException = error;
            }
            snapshotReady.Set();
        }

        private GrayFrameSnapshot CreateGrayFrameSnapshot(TucamFrame source, int maximumDimension)
        {
            int width = source.Width;
            int height = source.Height;
            int channels = Math.Max(1, (int)source.Channels);
            int elementBytes = Math.Max(1, (int)source.ElementBytes);
            int bytesPerPixel = channels * elementBytes;
            int stride = source.WidthStep > 0 ? checked((int)source.WidthStep) : width * bytesPerPixel;
            int imageSize = checked((int)source.ImageSize);
            if (source.Buffer == IntPtr.Zero || width <= 0 || height <= 0 || imageSize < stride * height)
                throw new InvalidOperationException("TUCam 帧缓冲区信息无效。");

            if (snapshotRawBuffer == null || snapshotRawBuffer.Length < imageSize)
                snapshotRawBuffer = new byte[imageSize];
            byte[] raw = snapshotRawBuffer;
            Marshal.Copy(IntPtr.Add(source.Buffer, source.HeaderSize), raw, 0, imageSize);
            int samplingStep = Math.Max(1, (Math.Max(width, height) + maximumDimension - 1) / maximumDimension);
            int outputWidth = (width + samplingStep - 1) / samplingStep;
            int outputHeight = (height + samplingStep - 1) / samplingStep;
            byte[] gray = new byte[outputWidth * outputHeight];

            for (int outputY = 0; outputY < outputHeight; outputY++)
            {
                int firstY = outputY * samplingStep;
                int lastY = Math.Min(height, firstY + samplingStep);
                for (int outputX = 0; outputX < outputWidth; outputX++)
                {
                    int firstX = outputX * samplingStep;
                    int lastX = Math.Min(width, firstX + samplingStep);
                    int sampleWidth = Math.Min(2, lastX - firstX);
                    int sampleHeight = Math.Min(2, lastY - firstY);
                    int sampleX = firstX + (lastX - firstX - sampleWidth) / 2;
                    int sampleY = firstY + (lastY - firstY - sampleHeight) / 2;
                    long sum = 0;
                    int count = 0;
                    for (int y = sampleY; y < sampleY + sampleHeight; y++)
                    {
                        // TUFRM_FMT_USUAl stores image rows bottom-up on Windows,
                        // while the preview and mouse coordinates use a top-left origin.
                        int row = (height - 1 - y) * stride;
                        for (int x = sampleX; x < sampleX + sampleWidth; x++)
                        {
                            int pixel = row + x * bytesPerPixel;
                            int channelSum = 0;
                            for (int channel = 0; channel < channels; channel++)
                            {
                                int component = pixel + channel * elementBytes;
                                channelSum += elementBytes == 1 ? raw[component] : raw[component + elementBytes - 1];
                            }
                            sum += channelSum / channels;
                            count++;
                        }
                    }
                    gray[outputY * outputWidth + outputX] = (byte)(sum / Math.Max(1, count));
                }
            }

            return new GrayFrameSnapshot(outputWidth, outputHeight, width, height, samplingStep, gray);
        }

        private void CancelSnapshotRequest()
        {
            lock (snapshotStateSync)
            {
                snapshotRequestId++;
                snapshotRequested = false;
            }
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
            UpdateSelectionOverlayFromImageCoordinates();
        }

        private void CameraShowForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            CancelRectangleDrawing();
            selectionOverlay = null;
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
