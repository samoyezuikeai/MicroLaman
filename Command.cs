using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace MicroLaman
{
    class Command
    {
        //得到当前位置
        public string GetPosition()
        {
            return SerialPortManager.SendAndReceive("?pos");
        }

        //得到x y z的长度单位
        public string GetDim()
        {
            return SerialPortManager.SendAndReceive("?dim");
        }

        //相对移动
        public string MoveRelative(double x, double y, double z)
        {
            string command = string.Format(CultureInfo.InvariantCulture, "mor {0} {1} {2}", x, y, z);
            return SerialPortManager.SendAndReceive(command);
        }

        //绝对移动
        public string MoveAbsolute(double x, double y, double z)
        {
            string command = string.Format(CultureInfo.InvariantCulture, "moa {0} {1} {2}", x, y, z);
            return SerialPortManager.SendAndReceive(command);
        }
    }
}
