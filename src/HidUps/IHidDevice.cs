using HidSharp.Reports;

namespace HidUps
{
    public interface IHidDevice
    {
        string DevicePath { get; }
        string GetManufacturer();
        string GetProductName();
        string GetSerialNumber();
        ReportDescriptor GetReportDescriptor();
        bool TryOpen(out IHidStream stream);
    }

    public interface IHidStream : IDisposable
    {
        int ReadTimeout { get; set; }
        int WriteTimeout { get; set; }
        void GetFeature(byte[] buffer);
        void SetFeature(byte[] buffer);
        void Write(byte[] buffer);
        void Read(byte[] buffer);
    }
}
