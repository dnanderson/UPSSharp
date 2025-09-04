using HidUps;
using Xunit;

namespace HidUps.UnitTests;

public class UpsDeviceTests
{
    [Fact]
    public void GetDeviceStatus_ShouldReturnString()
    {
        var upsDevice = new UpsDevice();
        var status = upsDevice.GetDeviceStatus();
        Assert.IsType<string>(status);
    }
}
