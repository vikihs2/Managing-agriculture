using System.IO.Ports;

namespace ManagingAgriculture.Services
{
    public class ArduinoService
    {
        private readonly SerialPort? _serialPort;
        private int _latestValue;
        private bool _isConnected = false;

        public ArduinoService()
        {
            try
            {
                _serialPort = new SerialPort("COM5", 9600);
                _serialPort.DtrEnable = true;
                _serialPort.RtsEnable = true;
                _serialPort.DataReceived += OnDataReceived;
                _serialPort.Open();
                _isConnected = true;
            }
            catch
            {
                _isConnected = false;
                // Arduino connection failed - will show disconnected page
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string line = _serialPort?.ReadLine().Trim() ?? string.Empty;
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

        public bool IsConnected()
        {
            return _isConnected;
        }
    }
}
