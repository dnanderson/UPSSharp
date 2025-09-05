using Moq;
using HidSharp;
using HidSharp.Reports;
using Xunit;

namespace HidUps.UnitTests
{
    public class GenericUpsTests
    {
        // This handcrafted HID Report Descriptor defines a theoretical UPS with multiple collections
        // to test the collection-aware parsing logic.
        // It defines one Feature report with ID 1.
        // The report contains three distinct 'Voltage' usages, one in each collection.
        private static readonly byte[] _multiCollectionReportDescriptor = new byte[]
        {
            0x05, 0x84,       // Usage Page (Power Device)
            0x09, 0x04,       // Usage (UPS)
            0xA1, 0x01,       // Collection (Application)
            0x85, 0x01,       //   Report ID (1)

            // --- Input Collection ---
            0x09, 0x1A,       //   Usage (Input Collection) - 0x1A from UpsUsage.cs
            0xA1, 0x02,       //   Collection (Logical)
                0x05, 0x84,   //     Usage Page (Power Device)
                0x09, 0x30,   //     Usage (Voltage) - 0x30 from UpsUsage.cs
                0x15, 0x00,   //     Logical Minimum (0)
                0x26, 0xFF, 0x00, // Logical Maximum (255)
                0x75, 0x08,   //     Report Size (8 bits)
                0x95, 0x01,   //     Report Count (1)
                0xB1, 0x02,   //     Feature (Data,Var,Abs) - 1st byte of report data
            0xC0,             //   End Collection

            // --- Output Collection ---
            0x09, 0x1C,       //   Usage (Output Collection) - 0x1C from UpsUsage.cs
            0xA1, 0x02,       //   Collection (Logical)
                0x05, 0x84,   //     Usage Page (Power Device)
                0x09, 0x30,   //     Usage (Voltage) - 0x30 from UpsUsage.cs
                0x15, 0x00,   //     Logical Minimum (0)
                0x26, 0xFF, 0x00, // Logical Maximum (255)
                0x75, 0x08,   //     Report Size (8 bits)
                0x95, 0x01,   //     Report Count (1)
                0xB1, 0x02,   //     Feature (Data,Var,Abs) - 2nd byte
            0xC0,             //   End Collection

            // --- Battery Collection ---
            0x09, 0x12,       //   Usage (Battery Collection) - 0x12 from UpsUsage.cs
            0xA1, 0x02,       //   Collection (Logical)
                // Battery Voltage
                0x05, 0x84,   //     Usage Page (Power Device)
                0x09, 0x30,   //     Usage (Voltage) - 0x30 from UpsUsage.cs
                0x15, 0x00,   //     Logical Minimum (0)
                0x26, 0xFF, 0x00, // Logical Maximum (255)
                0x75, 0x08,   //     Report Size (8 bits)
                0x95, 0x01,   //     Report Count (1)
                0xB1, 0x02,   //     Feature (Data,Var,Abs) - 3rd byte

                // Battery Charging Flag
                0x05, 0x85,   //     Usage Page (Battery System)
                0x09, 0x44,   //     Usage (Charging) - 0x44 from UpsUsage.cs
                0x15, 0x00,   //     Logical Minimum (0)
                0x25, 0x01,   //     Logical Maximum (1)
                0x75, 0x01,   //     Report Size (1 bit)
                0x95, 0x01,   //     Report Count (1)
                0xB1, 0x02,   //     Feature (Data,Var,Abs) - 4th byte, bit 0

                // Padding to make it byte-aligned
                0x75, 0x07,   //     Report Size (7 bits)
                0x95, 0x01,   //     Report Count (1)
                0xB1, 0x03,   //     Feature (Cnst,Var,Abs)
            0xC0,             //   End Collection

            0xC0              // End Collection (Application)
        };

        private GenericUps CreateTestUps(byte[] featureReportDescriptor, byte[] featureReportData)
        {
            var mockDevice = new Mock<IHidDevice>();
            var mockStream = new Mock<IHidStream>();

            var descriptor = new ReportDescriptor(featureReportDescriptor);

            mockDevice.Setup(d => d.GetReportDescriptor()).Returns(descriptor);

            mockStream.Setup(s => s.GetFeature(It.IsAny<byte[]>()))
                .Callback((byte[] buffer) => {
                    buffer[0] = 1; // Report ID
                    Array.Copy(featureReportData, 0, buffer, 1, featureReportData.Length);
                });

            var stream = mockStream.Object;
            mockDevice.Setup(d => d.TryOpen(out stream)).Returns(true);

            // The constructor does the parsing, so we call it here.
            var ups = new GenericUps(mockDevice.Object);

            return ups;
        }

        [Fact]
        public void CorrectlyDistinguishesVoltagesByCollection()
        {
            // Arrange: Report data with different voltage values
            // Input: 120, Output: 230, Battery: 12
            var reportData = new byte[] { 120, 230, 12, 0b0000_0000 };
            var ups = CreateTestUps(_multiCollectionReportDescriptor, reportData);

            // Act
            var inputVoltage = ups.InputVoltage;
            var outputVoltage = ups.OutputVoltage;
            var batteryVoltage = ups.BatteryVoltage;

            // Assert
            Assert.Equal(120, inputVoltage);
            Assert.Equal(230, outputVoltage);
            Assert.Equal(12, batteryVoltage);
        }

        [Theory]
        [InlineData(0b0000_0001, true)]  // Charging bit is set
        [InlineData(0b0000_0000, false)] // Charging bit is not set
        public void IsCharging_ReturnsCorrectValueFromBatteryCollection(byte batteryFlags, bool expected)
        {
            // Arrange: Report data with only the battery flags changing.
            var reportData = new byte[] { 0, 0, 0, batteryFlags };
            var ups = CreateTestUps(_multiCollectionReportDescriptor, reportData);

            // Act
            var isCharging = ups.IsCharging;

            // Assert
            Assert.Equal(expected, isCharging);
        }

        [Fact]
        public void GetValue_WhenUsageIsNotInCorrectCollection_ReturnsNull()
        {
            // Arrange: A descriptor that only has Voltage in the Input Collection.
            var simpleDescriptor = new byte[]
            {
                0x05, 0x84, 0x09, 0x04, 0xA1, 0x01, 0x85, 0x01,
                0x09, 0x1A, 0xA1, 0x02, 0x05, 0x84, 0x09, 0x30, 0x15, 0x00,
                0x26, 0xFF, 0x00, 0x75, 0x08, 0x95, 0x01, 0xB1, 0x02, 0xC0, 0xC0
            };
            var reportData = new byte[] { 120 };
            var ups = CreateTestUps(simpleDescriptor, reportData);

            // Act
            var inputVoltage = ups.InputVoltage;
            var outputVoltage = ups.OutputVoltage; // Should be null

            // Assert
            Assert.Equal(120, inputVoltage);
            Assert.Null(outputVoltage);
        }

        [Fact]
        public void GetFlag_WhenUsageIsNotInDescriptor_ReturnsNull()
        {
            // Arrange
            var reportData = new byte[] { 0, 0, 0, 0 };
            var ups = CreateTestUps(_multiCollectionReportDescriptor, reportData);

            // Act: This usage is not in our test descriptor at all.
            var isOverloaded = ups.IsOverloaded;

            // Assert
            Assert.Null(isOverloaded);
        }

        // This descriptor tests the parser's ability to walk a deeply nested
        // collection structure. The structure is:
        // App -> Logical (PowerSummary) -> Logical (RemainingEnergy) -> Physical (Battery)
        // The AudibleAlarmControl is inside the deepest collection.
        private static readonly byte[] _deeplyNestedReportDescriptor = new byte[]
        {
            0x05, 0x84,       // Usage Page (Power Device)
            0x09, 0x04,       // Usage (UPS)
            0xA1, 0x01,       // Collection (Application)
            0x85, 0x02,       //   Report ID (2)

            // --- Level 1: PowerSummary Collection ---
            0x09, 0x0D,       //   Usage (PowerSummary Collection)
            0xA1, 0x02,       //   Collection (Logical)

                // --- Level 2: RemainingEnergy Collection ---
                0x09, 0x11,   //     Usage (RemainingEnergy Collection)
                0xA1, 0x02,   //     Collection (Logical)

                    // --- Level 3: Battery Collection ---
                    0x09, 0x12,   //   Usage (Battery Collection)
                    0xA1, 0x00,   //   Collection (Physical)

                        // --- Target Usage: AudibleAlarmControl ---
                        0x05, 0x84,   //     Usage Page (Power Device)
                        0x09, 0xFE,   //     Usage (Audible Alarm Control)
                        0x15, 0x00,   //     Logical Minimum (0)
                        0x25, 0x02,   //     Logical Maximum (2) ; 0=disable, 1=enable, 2=mute
                        0x75, 0x08,   //     Report Size (8 bits)
                        0x95, 0x01,   //     Report Count (1)
                        0xB1, 0x02,   //     Feature (Data,Var,Abs)

                    0xC0,         //     End Collection (Physical)
                0xC0,             //   End Collection (Logical)
            0xC0,                 // End Collection (Logical)
            0xC0                  // End Collection (Application)
        };


        // This is an internal helper in GenericUps, so we need a way to invoke it for testing.
        // We create a derived class that exposes the method.
        private class TestableGenericUps : GenericUps
        {
            public TestableGenericUps(IHidDevice device) : base(device) { }

            public bool SetAudibleAlarm(int value)
            {
                // We are testing if SetValue can correctly find the usage path:
                // BatteryCollection -> AudibleAlarmControl
                return SetValue(new[] { UpsUsage.BatteryCollection, UpsUsage.AudibleAlarmControl }, value);
            }
        }

        [Fact]
        public void CorrectlyFindsUsageInDeeplyNestedCollection()
        {
            // Arrange
            var mockDevice = new Mock<IHidDevice>();
            var mockStream = new Mock<IHidStream>();
            var descriptor = new ReportDescriptor(_deeplyNestedReportDescriptor);

            mockDevice.Setup(d => d.GetReportDescriptor()).Returns(descriptor);

            // This variable will capture the data written to the device.
            byte[] writtenData = null;

            mockStream.Setup(s => s.SetFeature(It.IsAny<byte[]>()))
                .Callback((byte[] buffer) => {
                    writtenData = new byte[buffer.Length];
                    Array.Copy(buffer, writtenData, buffer.Length);
                });

            var stream = mockStream.Object;
            mockDevice.Setup(d => d.TryOpen(out stream)).Returns(true);

            var ups = new TestableGenericUps(mockDevice.Object);

            // Act
            // 1 = Enable Alarm. See descriptor.
            bool success = ups.SetAudibleAlarm(1);

            // Assert
            Assert.True(success, "SetValue should have returned true, indicating the usage was found.");
            Assert.NotNull(writtenData);
            Assert.Equal(2, writtenData.Length); // Report ID + 1 byte of data
            Assert.Equal(2, writtenData[0]);     // Report ID
            Assert.Equal(1, writtenData[1]);     // The value we wanted to write
        }
    }
}
