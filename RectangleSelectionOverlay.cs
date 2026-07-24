using System;
using System.Collections.Generic;
using System.Drawing;

namespace MicroLaman
{
    /// <summary>
    /// 保存并绘制与相机帧合成的框选区域、扫描网格和已访问点。
    /// </summary>
    internal sealed class RectangleSelectionOverlay
    {
        private readonly object stateSync = new object();
        private RectangleF normalizedSelection = RectangleF.Empty;
        private int xPointCount = 3;
        private int yPointCount = 3;
        private readonly List<PointF> recordedScanPoints = new List<PointF>();

        /// <summary>
        /// 更新预览控件归一化坐标中的框选区域。
        /// </summary>
        internal void SetSelection(RectangleF selection)
        {
            lock (stateSync)
                normalizedSelection = selection;
        }

        /// <summary>
        /// 清除当前框选区域。
        /// </summary>
        internal void ClearSelection()
        {
            SetSelection(RectangleF.Empty);
        }

        /// <summary>
        /// 设置网格在 X、Y 方向上的点数。
        /// </summary>
        internal void SetGridSize(int xCount, int yCount)
        {
            lock (stateSync)
            {
                xPointCount = Math.Max(1, xCount);
                yPointCount = Math.Max(1, yCount);
            }
        }

        /// <summary>
        /// 用归一化预览坐标更新扫描过程中记录的红点。
        /// </summary>
        internal void SetRecordedScanPoints(IEnumerable<PointF> points)
        {
            lock (stateSync)
            {
                recordedScanPoints.Clear();
                if (points != null)
                    recordedScanPoints.AddRange(points);
            }
        }

        /// <summary>
        /// 将全部标注直接绘制到当前相机预览帧表面。
        /// </summary>
        internal void Draw(Graphics graphics, Size clientSize)
        {
            RectangleF selection;
            int xCount;
            int yCount;
            List<PointF> recordedPoints;
            lock (stateSync)
            {
                selection = normalizedSelection;
                xCount = xPointCount;
                yCount = yPointCount;
                recordedPoints = new List<PointF>(recordedScanPoints);
            }

            if (!selection.IsEmpty && clientSize.Width > 0 && clientSize.Height > 0)
            {
                Rectangle rectangle = new Rectangle(
                    (int)Math.Round(selection.X * clientSize.Width),
                    (int)Math.Round(selection.Y * clientSize.Height),
                    (int)Math.Round(selection.Width * clientSize.Width),
                    (int)Math.Round(selection.Height * clientSize.Height));

                rectangle = Rectangle.Intersect(rectangle, new Rectangle(Point.Empty, clientSize));
                if (rectangle.Width >= 2 && rectangle.Height >= 2)
                {
                    rectangle.Width -= 1;
                    rectangle.Height -= 1;
                    using (Pen shadow = new Pen(Color.Black, 4f))
                    using (Pen border = new Pen(Color.DeepSkyBlue, 2f))
                    {
                        graphics.DrawRectangle(shadow, rectangle);
                        graphics.DrawRectangle(border, rectangle);
                    }

                    DrawScanPoints(graphics, rectangle, xCount, yCount);
                }
            }

            DrawRecordedScanPoints(graphics, clientSize, recordedPoints);
        }

        /// <summary>
        /// 绘制黄色目标网格点。
        /// </summary>
        private static void DrawScanPoints(Graphics graphics, Rectangle rectangle, int xCount, int yCount)
        {
            const int radius = 3;
            using (Brush pointBrush = new SolidBrush(Color.Yellow))
            using (Pen pointOutline = new Pen(Color.Black, 1f))
            {
                for (int yIndex = 0; yIndex < yCount; yIndex++)
                {
                    float y = yCount == 1
                        ? rectangle.Top + rectangle.Height / 2f
                        : rectangle.Top + yIndex * rectangle.Height / (float)(yCount - 1);

                    for (int xIndex = 0; xIndex < xCount; xIndex++)
                    {
                        float x = xCount == 1
                            ? rectangle.Left + rectangle.Width / 2f
                            : rectangle.Left + xIndex * rectangle.Width / (float)(xCount - 1);
                        RectangleF marker = new RectangleF(x - radius, y - radius, radius * 2, radius * 2);
                        graphics.FillEllipse(pointBrush, marker);
                        graphics.DrawEllipse(pointOutline, marker);
                    }
                }
            }
        }

        /// <summary>
        /// 绘制扫描后记录的红色实际到达点。
        /// </summary>
        private static void DrawRecordedScanPoints(Graphics graphics, Size clientSize, IList<PointF> points)
        {
            if (points.Count == 0)
                return;

            const float radius = 3f;
            using (Brush fill = new SolidBrush(Color.Red))
            using (Pen outline = new Pen(Color.White, 1f))
            {
                foreach (PointF point in points)
                {
                    float x = point.X * clientSize.Width;
                    float y = point.Y * clientSize.Height;
                    RectangleF marker = new RectangleF(x - radius, y - radius, radius * 2, radius * 2);
                    graphics.FillEllipse(fill, marker);
                    graphics.DrawEllipse(outline, marker);
                }
            }
        }
    }
}
