using System;
using System.Globalization;

namespace MicroLaman
{
    class Command
    {
        public StagePosition ReadPosition()
        {
            string response = SerialPortManager.SendAndReceive("?pos");
            string[] values = SplitResponse(response, 3, "读取平台位置");
            return new StagePosition
            {
                X = ParseNumber(values[0], "X 位置"),
                Y = ParseNumber(values[1], "Y 位置"),
                Z = ParseNumber(values[2], "Z 位置")
            };
        }

        public int[] ReadDimensions()
        {
            string response = SerialPortManager.SendAndReceive("?dim");
            string[] values = SplitResponse(response, 2, "读取平台坐标单位");
            return new[]
            {
                (int)ParseNumber(values[0], "X 单位"),
                (int)ParseNumber(values[1], "Y 单位")
            };
        }

        public string MoveAbsoluteXY(double x, double y)
        {
            string command = string.Format(
                CultureInfo.InvariantCulture,
                "moa {0:R} {1:R}",
                x,
                y);
            string response = SerialPortManager.SendAndReceive(command);
            if (string.IsNullOrWhiteSpace(response))
                throw new InvalidOperationException("平台移动没有返回完成状态。");
            if (response.IndexOf('E') >= 0 || response.IndexOf('S') >= 0 || response.IndexOf('L') >= 0)
                throw new InvalidOperationException("平台移动失败，TANGO 返回：" + response);
            return response;
        }

        private static string[] SplitResponse(string response, int minimumCount, string operation)
        {
            if (string.IsNullOrWhiteSpace(response))
                throw new InvalidOperationException(operation + "失败：串口没有返回数据。");
            string[] values = response.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length < minimumCount)
                throw new InvalidOperationException(operation + "失败：返回格式不正确（" + response + "）。");
            return values;
        }

        private static double ParseNumber(string text, string name)
        {
            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new InvalidOperationException(name + "不是有效数字：" + text);
            return value;
        }
    }
}
