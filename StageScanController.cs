using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace MicroLaman
{
    /// <summary>
    /// 表示平台三轴坐标；当前扫描只改变 X、Y，Z 始终保持原值。
    /// </summary>
    internal struct StagePosition
    {
        internal double X;
        internal double Y;
        internal double Z;
    }

    /// <summary>
    /// 保存平台 X/Y 位移到相机图像 X/Y 位移的二维线性标定矩阵。
    /// </summary>
    internal sealed class StagePixelCalibration
    {
        internal double PixelXPerStageX;
        internal double PixelYPerStageX;
        internal double PixelXPerStageY;
        internal double PixelYPerStageY;

        /// <summary>
        /// 将原点图像中的目标点换算成使其移动到视野中心的平台绝对坐标。
        /// </summary>
        internal StagePosition ImagePointToStage(
            PointF imagePoint,
            int imageWidth,
            int imageHeight,
            StagePosition origin)
        {
            double imageDeltaX = imagePoint.X - imageWidth / 2.0;
            double imageDeltaY = imagePoint.Y - imageHeight / 2.0;
            double determinant = GetDeterminant();
            if (Math.Abs(determinant) < 1e-9)
                throw new InvalidOperationException("X、Y 标定矩阵不可逆，无法计算平台坐标。");

            // 样品固定点在图像中的移动方向与平台视野中心移动方向相反。
            double stageDeltaX = -(PixelYPerStageY * imageDeltaX - PixelXPerStageY * imageDeltaY) / determinant;
            double stageDeltaY = -(-PixelYPerStageX * imageDeltaX + PixelXPerStageX * imageDeltaY) / determinant;
            return new StagePosition
            {
                X = origin.X + stageDeltaX,
                Y = origin.Y + stageDeltaY,
                Z = origin.Z
            };
        }

        /// <summary>
        /// 根据平台相对原点的实际位移计算样品图像相对原图的像素位移。
        /// </summary>
        internal PointF StageDeltaToImageShift(double stageDeltaX, double stageDeltaY)
        {
            return new PointF(
                (float)(PixelXPerStageX * stageDeltaX + PixelXPerStageY * stageDeltaY),
                (float)(PixelYPerStageX * stageDeltaX + PixelYPerStageY * stageDeltaY));
        }

        /// <summary>
        /// 计算二维标定矩阵的行列式，用于判断 X、Y 标定方向是否可区分。
        /// </summary>
        internal double GetDeterminant()
        {
            return PixelXPerStageX * PixelYPerStageY - PixelXPerStageY * PixelYPerStageX;
        }
    }

    /// <summary>
    /// 负责明场图像标定、标定数据保存以及基于平台绝对坐标的蛇形扫描。
    /// </summary>
    internal sealed class StageScanController
    {
        private readonly Command command = new Command();
        private StagePosition? savedOrigin;
        private StagePixelCalibration savedCalibration;
        private int[] savedDimensions;
        private int savedImageWidth;
        private int savedImageHeight;

        /// <summary>
        /// 获取当前控制器是否保存了可用于扫描的完整标定数据。
        /// </summary>
        internal bool HasCalibration
        {
            get
            {
                return savedOrigin.HasValue
                    && savedCalibration != null
                    && savedDimensions != null
                    && savedImageWidth > 0
                    && savedImageHeight > 0;
            }
        }

        /// <summary>
        /// 清除原点和像素比例；重新连接控制器或更换相机后必须调用。
        /// </summary>
        internal void ResetOrigin()
        {
            savedOrigin = null;
            savedCalibration = null;
            savedDimensions = null;
            savedImageWidth = 0;
            savedImageHeight = 0;
        }

        /// <summary>
        /// 在明场图像下重复移动 X、Y 轴，计算并保存像素与平台坐标的换算矩阵。
        /// </summary>
        internal void Calibrate(
            CameraShowForm camera,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            if (!SerialPortManager.IsOpen)
                throw new InvalidOperationException("请先连接 TANGO 控制器。");
            if (camera == null || camera.IsDisposed)
                throw new InvalidOperationException("请先打开相机窗口。");
            if (camera.CameraImageWidth <= 0 || camera.CameraImageHeight <= 0)
                throw new InvalidOperationException("相机尚未取得有效图像，无法执行标定。");

            ResetOrigin();
            int[] dimensions = command.ReadDimensions();
            StagePosition origin = command.ReadPosition();
            camera.PrepareForNewScan();

            double xDistance = GetCalibrationDistance(dimensions[0]);
            double yDistance = GetCalibrationDistance(dimensions[1]);
            PointF xCalibration = CalibrateAxisRepeated(
                camera, origin, dimensions, xDistance, 0, "X", progress, cancellationToken);
            PointF yCalibration = CalibrateAxisRepeated(
                camera, origin, dimensions, 0, yDistance, "Y", progress, cancellationToken);

            StagePixelCalibration calibration = new StagePixelCalibration
            {
                PixelXPerStageX = xCalibration.X,
                PixelYPerStageX = xCalibration.Y,
                PixelXPerStageY = yCalibration.X,
                PixelYPerStageY = yCalibration.Y
            };
            if (Math.Abs(calibration.GetDeterminant()) < 1e-6)
                throw new InvalidOperationException("图像标定失败：X、Y 两次移动得到的图像方向无法区分。");

            VerifyAtOrigin(origin, command.ReadPosition(), dimensions);
            savedOrigin = origin;
            savedCalibration = calibration;
            savedDimensions = (int[])dimensions.Clone();
            savedImageWidth = camera.CameraImageWidth;
            savedImageHeight = camera.CameraImageHeight;
            progress.Report("标定完成");
        }

        /// <summary>
        /// 使用已保存的标定矩阵按蛇形路径移动；全程只依赖绝对坐标和 ?pos，不读取图像纹理。
        /// </summary>
        internal void Scan(
            CameraShowForm camera,
            IList<PointF> normalizedPoints,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            if (!SerialPortManager.IsOpen)
                throw new InvalidOperationException("请先连接 TANGO 控制器。");
            if (!HasCalibration)
                throw new InvalidOperationException("尚未完成平台定标。请在关闭激光、打开照明后先点击“平台定标”。");
            if (normalizedPoints == null || normalizedPoints.Count == 0)
                throw new InvalidOperationException("扫描路径为空。");
            if (camera.CameraImageWidth != savedImageWidth || camera.CameraImageHeight != savedImageHeight)
                throw new InvalidOperationException("相机分辨率已在标定后改变，请重新执行平台定标。");

            int[] currentDimensions = command.ReadDimensions();
            if (currentDimensions[0] != savedDimensions[0] || currentDimensions[1] != savedDimensions[1])
                throw new InvalidOperationException("平台坐标单位已在标定后改变，请重新执行平台定标。");

            StagePosition origin = savedOrigin.Value;
            StagePixelCalibration calibration = savedCalibration;
            progress.Report("返回标定原位");
            ReturnToOrigin(camera, origin, savedDimensions);
            camera.PrepareForNewScan();

            try
            {
                for (int index = 0; index < normalizedPoints.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    PointF normalized = normalizedPoints[index];
                    PointF imagePoint = new PointF(
                        normalized.X * savedImageWidth,
                        normalized.Y * savedImageHeight);
                    StagePosition target = calibration.ImagePointToStage(
                        imagePoint,
                        savedImageWidth,
                        savedImageHeight,
                        origin);

                    progress.Report(string.Format("扫描 {0}/{1}", index + 1, normalizedPoints.Count));
                    StagePosition actual = MoveToAndVerify(target, savedDimensions);
                    PointF imageShift = calibration.StageDeltaToImageShift(
                        actual.X - origin.X,
                        actual.Y - origin.Y);

                    camera.SetTemporaryOverlayPixelOffset(imageShift.X, imageShift.Y);
                    camera.RecordScanVisitAtViewCenter(imageShift.X, imageShift.Y);
                }
            }
            finally
            {
                ReturnToOrigin(camera, origin, savedDimensions);
            }
        }

        /// <summary>
        /// 移动到绝对目标坐标并用 ?pos 校验；若首次未到容差范围则再执行一次绝对定位。
        /// </summary>
        private StagePosition MoveToAndVerify(StagePosition target, int[] dimensions)
        {
            command.MoveAbsoluteXY(target.X, target.Y);
            StagePosition actual = command.ReadPosition();
            if (IsAtTarget(target, actual, dimensions))
                return actual;

            command.MoveAbsoluteXY(target.X, target.Y);
            actual = command.ReadPosition();
            if (!IsAtTarget(target, actual, dimensions))
            {
                throw new InvalidOperationException(string.Format(
                    "平台未到达目标坐标：目标 ({0:F4}, {1:F4})，实际 ({2:F4}, {3:F4})。",
                    target.X,
                    target.Y,
                    actual.X,
                    actual.Y));
            }

            return actual;
        }

        /// <summary>
        /// 判断平台实测 X、Y 坐标是否处于目标坐标容差范围内。
        /// </summary>
        private static bool IsAtTarget(StagePosition target, StagePosition actual, int[] dimensions)
        {
            return Math.Abs(actual.X - target.X) <= GetPositionTolerance(dimensions[0])
                && Math.Abs(actual.Y - target.Y) <= GetPositionTolerance(dimensions[1]);
        }

        /// <summary>
        /// 对同一轴重复标定三次并取中位数，抑制偶发图像配准误差。
        /// </summary>
        private PointF CalibrateAxisRepeated(
            CameraShowForm camera,
            StagePosition origin,
            int[] dimensions,
            double deltaX,
            double deltaY,
            string axisName,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            const int sampleCount = 3;
            double[] pixelXPerUnit = new double[sampleCount];
            double[] pixelYPerUnit = new double[sampleCount];
            for (int sample = 0; sample < sampleCount; sample++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress.Report(string.Format("标定 {0} {1}/{2}", axisName, sample + 1, sampleCount));
                CalibrationAxis result = CalibrateAxis(
                    camera, origin, deltaX, deltaY, cancellationToken);
                VerifyAtOrigin(origin, command.ReadPosition(), dimensions);
                double actualAxisDelta = Math.Abs(deltaX) > 0
                    ? result.ActualDeltaX
                    : result.ActualDeltaY;
                pixelXPerUnit[sample] = result.Translation.X / actualAxisDelta;
                pixelYPerUnit[sample] = result.Translation.Y / actualAxisDelta;
            }

            return new PointF(
                (float)MedianOfThree(pixelXPerUnit[0], pixelXPerUnit[1], pixelXPerUnit[2]),
                (float)MedianOfThree(pixelYPerUnit[0], pixelYPerUnit[1], pixelYPerUnit[2]));
        }

        /// <summary>
        /// 沿指定轴移动一次，通过移动前后两帧图像计算像素比例和方向。
        /// </summary>
        private CalibrationAxis CalibrateAxis(
            CameraShowForm camera,
            StagePosition origin,
            double deltaX,
            double deltaY,
            CancellationToken cancellationToken)
        {
            GrayFrameSnapshot reference = camera.CaptureGrayFrame(2, 15000, cancellationToken);
            bool moved = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                moved = true;
                command.MoveAbsoluteXY(origin.X + deltaX, origin.Y + deltaY);
                StagePosition reached = command.ReadPosition();
                GrayFrameSnapshot shifted = camera.CaptureGrayFrame(2, 15000, cancellationToken);
                ImageTranslation translation = ImageRegistration.MeasureTranslation(reference, shifted);

                double magnitude = Math.Sqrt(translation.X * translation.X + translation.Y * translation.Y);
                double maximum = Math.Min(reference.OriginalWidth, reference.OriginalHeight) * 0.30;
                if (translation.Confidence < 8 || magnitude < 4)
                    throw new InvalidOperationException(
                        "图像纹理位移过小或不清晰，无法可靠标定。请关闭激光并打开明场照明后重试。");
                if (magnitude > maximum)
                    throw new InvalidOperationException(
                        "标定位移超过视野的 30%，为保证前后图像仍有重叠区域，标定已停止。");

                double actualAxisDelta = Math.Abs(deltaX) > 0
                    ? reached.X - origin.X
                    : reached.Y - origin.Y;
                double requestedAxisDelta = Math.Abs(deltaX) > 0 ? deltaX : deltaY;
                if (Math.Abs(actualAxisDelta) < Math.Abs(requestedAxisDelta) * 0.5)
                    throw new InvalidOperationException("平台实际标定位移过小，可能已经接近软限位。");

                camera.SetTemporaryOverlayPixelOffset((float)translation.X, (float)translation.Y);
                return new CalibrationAxis
                {
                    Translation = translation,
                    ActualDeltaX = reached.X - origin.X,
                    ActualDeltaY = reached.Y - origin.Y
                };
            }
            finally
            {
                if (moved)
                {
                    try
                    {
                        command.MoveAbsoluteXY(origin.X, origin.Y);
                        camera.WaitForFreshFrames(2, 10000, CancellationToken.None);
                    }
                    finally
                    {
                        camera.SetTemporaryOverlayPixelOffset(0, 0);
                    }
                }
            }
        }

        /// <summary>
        /// 将平台移回定标原点、校验坐标并把框选标注恢复到原始位置。
        /// </summary>
        private void ReturnToOrigin(CameraShowForm camera, StagePosition origin, int[] dimensions)
        {
            command.MoveAbsoluteXY(origin.X, origin.Y);
            StagePosition actual = command.ReadPosition();
            VerifyAtOrigin(origin, actual, dimensions);
            camera.SetTemporaryOverlayPixelOffset(0, 0);
        }

        /// <summary>
        /// 验证平台实测位置是否已经返回保存的定标原点。
        /// </summary>
        private static void VerifyAtOrigin(StagePosition origin, StagePosition actual, int[] dimensions)
        {
            if (!IsAtTarget(origin, actual, dimensions))
                throw new InvalidOperationException("返回定标原位失败，请检查平台状态和软限位。");
        }

        /// <summary>
        /// 根据控制器坐标单位返回平台位置比较容差。
        /// </summary>
        private static double GetPositionTolerance(int dimension)
        {
            return dimension == 1 || dimension == 10 ? 0.2 : 0.0002;
        }

        /// <summary>
        /// 根据控制器坐标单位选择约 10 微米的安全标定距离。
        /// </summary>
        private static double GetCalibrationDistance(int dimension)
        {
            switch (dimension)
            {
                case 1:
                case 10:
                    return 10.0;
                case 2:
                case 9:
                    return 0.01;
                default:
                    throw new InvalidOperationException(
                        "自动标定仅支持 X、Y 使用 mm 或 μm 单位（dim 1、2、9、10）。");
            }
        }

        /// <summary>
        /// 返回三个数值的中位数。
        /// </summary>
        private static double MedianOfThree(double first, double second, double third)
        {
            return first + second + third
                - Math.Min(first, Math.Min(second, third))
                - Math.Max(first, Math.Max(second, third));
        }

        /// <summary>
        /// 保存单轴标定得到的图像位移和平台实际位移。
        /// </summary>
        private struct CalibrationAxis
        {
            internal ImageTranslation Translation;
            internal double ActualDeltaX;
            internal double ActualDeltaY;
        }
    }
}
