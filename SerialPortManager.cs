using System;
using System.IO.Ports;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroLaman
{
    public static class SerialPortManager
    {
        public static SerialPort serialPort { get; } = new SerialPort
        {
            BaudRate = 57600,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.Two,
            Handshake = Handshake.None,

            Encoding = Encoding.ASCII,
            NewLine = "\r",
        };

        public static void Open(string portName)
        {
            Close();

            serialPort.PortName = portName;
            serialPort.Open();

            serialPort.DiscardInBuffer();
            serialPort.DiscardOutBuffer();
        }

        public static void Close()
        {
            if (serialPort.IsOpen)
            {
                serialPort.Close();
            }
        }

        public static void Send(string command)
        {
            if (!serialPort.IsOpen) return;

            serialPort.WriteLine(command);
        }

        public static string SendAndReceive(string command)
        {
            if (!serialPort.IsOpen) return null;

            serialPort.WriteLine(command);
            return serialPort.ReadLine().Trim();
        }
    }
}
