using System.IO.Ports;

namespace ManagingAgriculture.Services
{
    public class ArduinoService
    {
        private SerialPort? _serialPort;
        private int _latestValue;
        private bool _isConnected = false;

        public ArduinoService()
        {
            try
            {
                _serialPort = new SerialPort("COM5", 9600);
                _serialPort.DataReceived += OnDataReceived;
                _serialPort.Open();
                _isConnected = true;
            }
            catch
            {
                // Arduino not connected, port unavailable, or other error
                _serialPort = null;
                _isConnected = false;
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort != null)
                {
                    string line = _serialPort.ReadLine();
                    if (int.TryParse(line, out int value))
                    {
                        _latestValue = value;
                    }
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
            try
            {
                return _serialPort != null && _serialPort.IsOpen;
            }
            catch
            {
                return false;
            }
        }
    }
}
