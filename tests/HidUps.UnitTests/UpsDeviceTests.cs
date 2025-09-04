using HidUps;
using System.Collections.Generic;
using Xunit;

namespace HidUps.UnitTests;

public class UpsDeviceTests
{
    [Fact]
    public void FindUpsDevices_ShouldReturnIEnumerableOfUps()
    {
        var devices = UpsDevice.FindUpsDevices();
        Assert.IsAssignableFrom<IEnumerable<Ups>>(devices);
    }
}
