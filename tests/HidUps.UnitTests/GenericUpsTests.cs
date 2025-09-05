using Moq;
using HidSharp;
using HidSharp.Reports;

namespace HidUps.UnitTests
{
    public class GenericUpsTests
    {
        // This is a handcrafted HID Report Descriptor for a theoretical UPS.
        // It defines one Feature report with ID 1.
        // The report contains:
        // 1. An 8-bit variable item (a bitfield) for status flags.
        // 2. An 8-bit value for RemainingCapacity.
        private static readonly byte[] _testReportDescriptor = new byte[]
        {
            0x05, 0x84,                    // Usage Page (Power Device)
            0x09, 0x04,                    // Usage (UPS)
            0xA1, 0x01,                    // Collection (Application)
            0x85, 0x01,                    //   Report ID (1)

            // --- Feature Report: 8 flags (1 byte) ---
            0x05, 0x85,                    //   Usage Page (Battery System)
            0x09, (byte)((uint)UpsUsage.Charging & 0xFF),        //   Usage (Charging) - bit 0
            0x09, (byte)((uint)UpsUsage.Discharging & 0xFF),      //   Usage (Discharging) - bit 1
            0x09, (byte)((uint)UpsUsage.FullyCharged & 0xFF),     //   Usage (FullyCharged) - bit 2
            0x09, (byte)((uint)UpsUsage.NeedReplacement & 0xFF),  //   Usage (NeedReplacement) - bit 3
            0x09, (byte)((uint)UpsUsage.AcPresent & 0xFF),        //   Usage (AcPresent) - bit 4
            0x09, 0x01,                    //   Usage (Dummy 1) - bit 5
            0x09, 0x02,                    //   Usage (Dummy 2) - bit 6
            0x09, 0x03,                    //   Usage (Dummy 3) - bit 7
            0x15, 0x00,                    //   Logical Minimum (0)
            0x25, 0x01,                    //   Logical Maximum (1)
            0x75, 0x01,                    //   Report Size (1 bit)
            0x95, 0x08,                    //   Report Count (8 flags)
            0xB1, 0x02,                    //   Feature (Data,Var,Abs)

            // --- Feature Report: one 8-bit value (1 byte) ---
            0x05, 0x85,                    //   Usage Page (Battery System)
            0x09, (byte)((uint)UpsUsage.RemainingCapacity & 0xFF), // Usage (RemainingCapacity)
            0x15, 0x00,                    //   Logical Minimum (0)
            0x25, 0x64,                    //   Logical Maximum (100) -> 100 in decimal
            0x75, 0x08,                    //   Report Size (8 bits)
            0x95, 0x01,                    //   Report Count (1)
            0xB1, 0x02,                    //   Feature (Data,Var,Abs)

            0xC0                           // End Collection
        };

        private GenericUps CreateTestUps(byte[] featureReportData)
        {
            var mockDevice = new Mock<IHidDevice>();
            var mockStream = new Mock<IHidStream>();

            var descriptor = new ReportDescriptor(_testReportDescriptor);

            mockDevice.Setup(d => d.GetReportDescriptor()).Returns(descriptor);

            mockStream.Setup(s => s.GetFeature(It.IsAny<byte[]>()))
                .Callback((byte[] buffer) => {
                    buffer[0] = 1; // Report ID
                    Array.Copy(featureReportData, 0, buffer, 1, featureReportData.Length);
                });

            var stream = mockStream.Object;
            mockDevice.Setup(d => d.TryOpen(out stream)).Returns(true);

            var ups = new GenericUps(mockDevice.Object);

            return ups;
        }

        [Theory]
        [InlineData(0b0001_0000, true)]  // AC Present (bit 4) is 1 -> OnBattery is false
        [InlineData(0b0000_0000, false)] // AC Present (bit 4) is 0 -> OnBattery is true
        public void IsOnBattery_ReturnsCorrectValue(byte flags, bool expectedAcPresent)
        {
            var reportData = new byte[] { flags, 0x00 };
            var ups = CreateTestUps(reportData);

            var isOnBattery = ups.IsOnBattery;

            Assert.Equal(!expectedAcPresent, isOnBattery);
        }

        [Theory]
        [InlineData(0b0000_0001, true)]  // Charging (bit 0) is 1
        [InlineData(0b0000_0000, false)] // Charging (bit 0) is 0
        public void IsCharging_ReturnsCorrectValue(byte flags, bool expected)
        {
            var reportData = new byte[] { flags, 0x00 };
            var ups = CreateTestUps(reportData);

            var isCharging = ups.IsCharging;

            Assert.Equal(expected, isCharging);
        }

        [Theory]
        [InlineData(0b0000_0100, true)]  // Fully Charged (bit 2) is 1
        [InlineData(0b0000_0000, false)] // Fully Charged (bit 2) is 0
        public void IsFullyCharged_ReturnsCorrectValue(byte flags, bool expected)
        {
            var reportData = new byte[] { flags, 0x00 };
            var ups = CreateTestUps(reportData);

            var isFullyCharged = ups.IsFullyCharged;

            Assert.Equal(expected, isFullyCharged);
        }

        [Theory]
        [InlineData(50, 50.0)] // 50%
        [InlineData(100, 100.0)] // 100%
        [InlineData(0, 0.0)] // 0%
        public void RemainingCapacityPercent_ReturnsCorrectValue(byte capacityValue, double expected)
        {
            var reportData = new byte[] { 0x00, capacityValue };
            var ups = CreateTestUps(reportData);

            var remainingCapacity = ups.RemainingCapacityPercent;

            Assert.Equal(expected, remainingCapacity);
        }

        [Fact]
        public void GetFlag_WhenUsageIsNotInDescriptor_ReturnsNull()
        {
            var reportData = new byte[] { 0x00, 0x00 };
            var ups = CreateTestUps(reportData);

            // This usage is not in our test descriptor
            var isOverloaded = ups.IsOverloaded;

            Assert.Null(isOverloaded);
        }
    }
}
