using HidSharp;

namespace HidUps;

public class UpsDevice
{
    public string? GetDeviceStatus()
    {
        var list = DeviceList.Local;
        var hidDevice = list.GetHidDevices(vendorID: 0x0463, productID: 0xFFFF).FirstOrDefault();

        if (hidDevice != null)
        {
            return "Device found";
        }

        return "Device not found";
    }
}
