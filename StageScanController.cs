using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace MicroLaman
{
    internal struct StagePosition
    {
        internal double X;
        internal double Y;
        internal double Z;
    }

    internal sealed class StagePixelCalibration
    {
        internal double PixelXPerStageX;
        internal double PixelYPerStageX;
        internal double PixelXPerStageY;
        internal double PixelYPerStageY;

        internal PointF StageDeltaToImage(double deltaX, double deltaY)
        {
            return new PointF(
                (float)(PixelXPerStageX * deltaX + PixelXPerStageY * deltaY),
                (float)(PixelYPerStageX * deltaX + PixelYPerStageY * deltaY));
        }

        internal StagePosition ImagePointToStage(PointF imagePoint, int imageWidth, int imageHeight, StagePosition origin)
        {
            double imageDeltaX = imagePoint.X - imageWidth / 2.0;
            double imageDeltaY = imagePoint.Y - imageHeight / 2.0;
            double determinant = PixelXPerStageX * PixelYPerStageY - PixelXPerStageY * PixelYPerStageX;
            if (Math.Abs(determinant) < 1e-9)
                throw new InvalidOperationException("X、Y 标定结果共线，无法计算二维平台坐标。");

            // A fixed specimen point shifts opposite to the stage center movement.
            double stageDeltaX = -(PixelYPerStageY * imageDeltaX - PixelXPerStageY * imageDeltaY) / determinant;
            double stageDeltaY = -(-PixelYPerStageX * imageDeltaX + PixelXPerStageX * imageDeltaY) / determinant;
            return new StagePosition { X = origin.X + stageDeltaX, Y = origin.Y + stageDeltaY, Z = origin.Z };
        }

        internal PointF ImageShiftToStageDelta(double imageShiftX, double imageShiftY)
        {
            double determinant = PixelXPerStageX * PixelYPerStageY - PixelXPerStageY * PixelYPerStageX;
            if (Math.Abs(determinant) < 1e-9)
                throw new InvalidOperationException("X、Y 标定矩阵不可逆，无法修正移动偏差。");

            return new PointF(
                (float)((PixelYPerStageY * imageShiftX - PixelXPerStageY * imageShiftY) / determinant),
                (float)((-PixelYPerStageX * imageShiftX + PixelXPerStageX * imageShiftY) / determinant));
        }

        internal void RefineFromObservation(double stageDeltaX, double stageDeltaY, double measuredX, double measuredY)
        {
            double lengthSquared = stageDeltaX * stageDeltaX + stageDeltaY * stageDeltaY;
            if (lengthSquared < 1e-12)
                return;

            double predictedX = PixelXPerStageX * stageDeltaX + PixelXPerStageY * stageDeltaY;
            double predictedY = PixelYPerStageX * stageDeltaX + PixelYPerStageY * stageDeltaY;
            const double learningRate = 0.15;
            double factorX = learningRate * (measuredX - predictedX) / lengthSquared;
            double factorY = learningRate * (measuredY - predictedY) / lengthSquared;
            PixelXPerStageX += factorX * stageDeltaX;
            PixelXPerStageY += factorX * stageDeltaY;
            PixelYPerStageX += factorY * stageDeltaX;
            PixelYPerStageY += factorY * stageDeltaY;
        }
    }

    internal sealed class StageScanController
    {
        private readonly Command command = new Command();
        private StagePosition? savedOrigin;

        internal void ResetOrigin()
        {
            savedOrigin = null;
        }

        internal void CalibrateAndScan(
            CameraShowForm camera,
            IList<PointF> normalizedPoints,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            if (!SerialPortManager.IsOpen)
                throw new InvalidOperationException("请先连接 TANGO 串口。");
            if (normalizedPoints == null || normalizedPoints.Count == 0)
                throw new InvalidOperationException("扫描路径为空。");

            int[] dimensions = command.ReadDimensions();
            StagePosition origin;
            if (savedOrigin.HasValue)
            {
                progress.Report("返回原位");
                ReturnToOrigin(camera, savedOrigin.Value, dimensions);
                origin = savedOrigin.Value;
            }
            else
            {
                origin = command.ReadPosition();
                savedOrigin = origin;
            }

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

            double determinant = calibration.PixelXPerStageX * calibration.PixelYPerStageY
                - calibration.PixelXPerStageY * calibration.PixelYPerStageX;
            if (Math.Abs(determinant) < 1e-6)
                throw new InvalidOperationException("图像标定失败：X、Y 两次移动得到的图像方向无法区分。");

            camera.BeginStageTracking(origin, calibration);
            int imageWidth = camera.CameraImageWidth;
            int imageHeight = camera.CameraImageHeight;
            GrayFrameSnapshot scanOriginFrame = camera.CaptureGrayFrame(2, 15000, cancellationToken);
            PreparedImageRegistration scanRegistration = ImageRegistration.Prepare(scanOriginFrame);
            GrayFrameSnapshot liveTrackingOrigin = camera.CaptureGrayFrame(0, 10000, cancellationToken, 384);
            camera.BeginLiveOverlayTracking(liveTrackingOrigin);
            double pointSpacing = GetMinimumPointSpacing(normalizedPoints, imageWidth, imageHeight);
            double tolerance = Math.Max(1.5, Math.Min(5.0, pointSpacing * 0.20));
            double searchRadius = Math.Max(64, Math.Min(160, pointSpacing * 4));
            try
            {
                for (int index = 0; index < normalizedPoints.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    PointF normalized = normalizedPoints[index];
                    PointF imagePoint = new PointF(normalized.X * imageWidth, normalized.Y * imageHeight);
                    StagePosition target = calibration.ImagePointToStage(imagePoint, imageWidth, imageHeight, origin);
                    progress.Report(string.Format("扫描 {0}/{1}", index + 1, normalizedPoints.Count));
                    command.MoveAbsoluteXY(target.X, target.Y);
                    StagePosition actual = command.ReadPosition();

                    double expectedX = imageWidth / 2.0 - imagePoint.X;
                    double expectedY = imageHeight / 2.0 - imagePoint.Y;
                    ImageTranslation actualTranslation = new ImageTranslation();
                    bool reachedTarget = false;
                    for (int correctionAttempt = 0; correctionAttempt <= 2; correctionAttempt++)
                    {
                        GrayFrameSnapshot currentFrame = camera.CaptureGrayFrame(1, 10000, cancellationToken);
                        actualTranslation = ImageRegistration.MeasureTranslationNear(
                            scanRegistration,
                            currentFrame,
                            expectedX,
                            expectedY,
                            searchRadius);
                        if (actualTranslation.Confidence < 5)
                            throw new InvalidOperationException("当前画面纹理不足，无法可靠判断移动位置，平台已停止继续扫描。");

                        double residualX = expectedX - actualTranslation.X;
                        double residualY = expectedY - actualTranslation.Y;
                        double error = Math.Sqrt(residualX * residualX + residualY * residualY);
                        if (error <= tolerance)
                        {
                            reachedTarget = true;
                            break;
                        }

                        if (correctionAttempt == 2)
                            break;

                        PointF correction = calibration.ImageShiftToStageDelta(residualX, residualY);
                        double correctionX = ClampCorrection(correction.X, xDistance);
                        double correctionY = ClampCorrection(correction.Y, yDistance);
                        progress.Report(string.Format("修正 {0}/{1}", index + 1, normalizedPoints.Count));
                        command.MoveAbsoluteXY(actual.X + correctionX, actual.Y + correctionY);
                        actual = command.ReadPosition();
                    }

                    // Use the measured image displacement so the rectangle stays locked
                    // to the specimen even when the stage has small positioning errors.
                    camera.SetTemporaryOverlayPixelOffset(
                        (float)actualTranslation.X,
                        (float)actualTranslation.Y);
                    camera.RecordScanVisitAtViewCenter(
                        (float)actualTranslation.X,
                        (float)actualTranslation.Y);

                    if (!reachedTarget)
                    {
                        throw new InvalidOperationException(string.Format(
                            "网格点 {0}/{1} 经过 2 次补偿后仍未达到精度要求（允许 {2:F1} px）。已记录红点并返回原位。",
                            index + 1,
                            normalizedPoints.Count,
                            tolerance));
                    }

                    calibration.RefineFromObservation(
                        actual.X - origin.X,
                        actual.Y - origin.Y,
                        actualTranslation.X,
                        actualTranslation.Y);
                }
            }
            finally
            {
                ReturnToOrigin(camera, origin, dimensions);
            }
        }

        private static double ClampCorrection(double correction, double calibrationDistance)
        {
            double limit = Math.Abs(calibrationDistance) * 2;
            return Math.Max(-limit, Math.Min(limit, correction));
        }

        private static double GetMinimumPointSpacing(IList<PointF> points, int imageWidth, int imageHeight)
        {
            double minimum = double.MaxValue;
            for (int index = 1; index < points.Count; index++)
            {
                double deltaX = (points[index].X - points[index - 1].X) * imageWidth;
                double deltaY = (points[index].Y - points[index - 1].Y) * imageHeight;
                double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                if (distance > 0.01)
                    minimum = Math.Min(minimum, distance);
            }

            return minimum == double.MaxValue ? 20 : minimum;
        }

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
                CalibrationAxis result = CalibrateAxis(camera, origin, deltaX, deltaY, cancellationToken);
                VerifyAtOrigin(origin, dimensions);
                double actualAxisDelta = Math.Abs(deltaX) > 0 ? result.ActualDeltaX : result.ActualDeltaY;
                pixelXPerUnit[sample] = result.Translation.X / actualAxisDelta;
                pixelYPerUnit[sample] = result.Translation.Y / actualAxisDelta;
            }

            return new PointF(
                (float)MedianOfThree(pixelXPerUnit[0], pixelXPerUnit[1], pixelXPerUnit[2]),
                (float)MedianOfThree(pixelYPerUnit[0], pixelYPerUnit[1], pixelYPerUnit[2]));
        }

        private static double MedianOfThree(double first, double second, double third)
        {
            return first + second + third
                - Math.Min(first, Math.Min(second, third))
                - Math.Max(first, Math.Max(second, third));
        }

        private void ReturnToOrigin(CameraShowForm camera, StagePosition origin, int[] dimensions)
        {
            command.MoveAbsoluteXY(origin.X, origin.Y);
            StagePosition actual = command.ReadPosition();
            VerifyAtOrigin(origin, actual, dimensions);

            camera.UpdateTrackedStagePosition(actual);
            camera.WaitForFreshFrames(2, 10000, CancellationToken.None);
            camera.SetTemporaryOverlayPixelOffset(0, 0);
        }

        private void VerifyAtOrigin(StagePosition origin, int[] dimensions)
        {
            VerifyAtOrigin(origin, command.ReadPosition(), dimensions);
        }

        private static void VerifyAtOrigin(StagePosition origin, StagePosition actual, int[] dimensions)
        {
            double xTolerance = GetPositionTolerance(dimensions[0]);
            double yTolerance = GetPositionTolerance(dimensions[1]);
            if (Math.Abs(actual.X - origin.X) > xTolerance || Math.Abs(actual.Y - origin.Y) > yTolerance)
                throw new InvalidOperationException("返回扫描原位失败，请检查平台状态和软限位。");
        }

        private static double GetPositionTolerance(int dimension)
        {
            return dimension == 1 || dimension == 10 ? 0.2 : 0.0002;
        }

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
                    throw new InvalidOperationException("图像纹理位移过小或不清晰，无法可靠标定。请降低曝光或增大样品纹理对比度后重试。");
                if (magnitude > maximum)
                    throw new InvalidOperationException("校准位移超过视野的 30%，为保证原点仍在重叠画面内已停止。");

                double actualAxisDelta = Math.Abs(deltaX) > 0
                    ? reached.X - origin.X
                    : reached.Y - origin.Y;
                double requestedAxisDelta = Math.Abs(deltaX) > 0 ? deltaX : deltaY;
                if (Math.Abs(actualAxisDelta) < Math.Abs(requestedAxisDelta) * 0.5)
                    throw new InvalidOperationException("平台实际校准位移过小，可能已接近软限位。");

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

        private static double GetCalibrationDistance(int dimension)
        {
            switch (dimension)
            {
                case 1:
                case 10:
                    return 10.0;   // 10 micrometres
                case 2:
                case 9:
                    return 0.01;   // 0.01 mm = 10 micrometres
                default:
                    throw new InvalidOperationException("自动标定仅支持 X、Y 使用 mm 或 µm 单位（dim 1、2、9、10）。");
            }
        }

        private struct CalibrationAxis
        {
            internal ImageTranslation Translation;
            internal double ActualDeltaX;
            internal double ActualDeltaY;
        }
    }
}
