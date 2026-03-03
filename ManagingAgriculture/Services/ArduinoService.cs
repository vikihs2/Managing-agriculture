using System.IO.Ports;

namespace ManagingAgriculture.Services
{
    public class ArduinoService
    {
        private readonly SerialPort _serialPort;
        private int _latestValue;

        public ArduinoService()
        {
            _serialPort = new SerialPort("COM5", 9600);
            _serialPort.DtrEnable = true;
            _serialPort.RtsEnable = true;
            _serialPort.DataReceived += OnDataReceived;
            _serialPort.Open();
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string line = _serialPort.ReadLine().Trim();
                if (int.TryParse(line, out int value))
                {
                    _latestValue = value;
                }
            }
            catch { }
        }

        public int GetValue()
        {
            return _latestValue;
        }
    }
}
