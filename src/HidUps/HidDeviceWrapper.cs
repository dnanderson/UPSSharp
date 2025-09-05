using HidSharp;
using HidSharp.Reports;

namespace HidUps
{
    public class HidDeviceWrapper : IHidDevice
    {
        private readonly HidDevice _device;

        public HidDeviceWrapper(HidDevice device)
        {
            _device = device;
        }

        public string DevicePath => _device.DevicePath;

        public string GetManufacturer() => _device.GetManufacturer();
        public string GetProductName() => _device.GetProductName();
        public string GetSerialNumber() => _device.GetSerialNumber();
        public ReportDescriptor GetReportDescriptor() => _device.GetReportDescriptor();

        public bool TryOpen(out IHidStream stream)
        {
            HidStream hidStream;
            if (_device.TryOpen(out hidStream))
            {
                stream = new HidStreamWrapper(hidStream);
                return true;
            }
            stream = null;
            return false;
        }
    }

    public class HidStreamWrapper : IHidStream
    {
        private readonly HidStream _stream;

        public HidStreamWrapper(HidStream stream)
        {
            _stream = stream;
        }

        public int ReadTimeout
        {
            get => _stream.ReadTimeout;
            set => _stream.ReadTimeout = value;
        }

        public int WriteTimeout
        {
            get => _stream.WriteTimeout;
            set => _stream.WriteTimeout = value;
        }

        public void Dispose() => _stream.Dispose();
        public void GetFeature(byte[] buffer) => _stream.GetFeature(buffer);
        public void SetFeature(byte[] buffer) => _stream.SetFeature(buffer);
        public void Write(byte[] buffer) => _stream.Write(buffer);
        public void Read(byte[] buffer) => _stream.Read(buffer);
    }
}
