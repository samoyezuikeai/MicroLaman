using System;
using System.IO.Ports;
using System.Text;

namespace MicroLaman
{
    public static class SerialPortManager
    {
        private static readonly object serialSync = new object();

        public static bool IsOpen
        {
            get
            {
                lock (serialSync)
                    return serialPort.IsOpen;
            }
        }

        public static SerialPort serialPort { get; } = new SerialPort
        {
            BaudRate = 57600,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.Two,
            Handshake = Handshake.None,

            Encoding = Encoding.ASCII,
            NewLine = "\r",
            ReadTimeout = 30000,
            WriteTimeout = 5000,
        };

        public static void Open(string portName)
        {
            lock (serialSync)
            {
                CloseCore();
                serialPort.PortName = portName;
                serialPort.Open();
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();
            }
        }

        public static void Close()
        {
            lock (serialSync)
                CloseCore();
        }

        public static string SendAndReceive(string command)
        {
            lock (serialSync)
            {
                if (!serialPort.IsOpen) return null;
                serialPort.DiscardInBuffer();
                serialPort.WriteLine(command);
                return serialPort.ReadLine().Trim();
            }
        }

        private static void CloseCore()
        {
            if (serialPort.IsOpen)
                serialPort.Close();
        }
    }
}
