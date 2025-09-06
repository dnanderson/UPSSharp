using Moq;
using HidSharp;
using HidSharp.Reports;
using Xunit;

namespace HidUps.UnitTests
{
    public class GenericUpsTests
    {
        // This handcrafted HID Report Descriptor defines a theoretical UPS with nested collections
        // to test the collection-aware parsing logic.
        // Structure: UPS → PowerConverter → Input/Output → Voltage
        private static readonly byte[] _nestedCollectionReportDescriptor = new byte[]
        {
            0x05, 0x84,       // Usage Page (Power Device)
            0x09, 0x04,       // Usage (UPS)
            0xA1, 0x01,       // Collection (Application)
            0x85, 0x01,       //   Report ID (1)

            // --- PowerConverter Collection ---
            0x09, 0x16,       //   Usage (PowerConverter) - 0x16 from UpsUsage.cs
            0xA1, 0x02,       //   Collection (Logical)
            
                // --- Nested Input Collection within PowerConverter ---
                0x09, 0x1A,       //     Usage (Input Collection) - 0x1A from UpsUsage.cs
                0xA1, 0x02,       //     Collection (Logical)
                    0x05, 0x84,   //       Usage Page (Power Device)
                    0x09, 0x30,   //       Usage (Voltage) - 0x30 from UpsUsage.cs
                    0x15, 0x00,   //       Logical Minimum (0)
                    0x26, 0xFF, 0x00, //   Logical Maximum (255)
                    0x75, 0x08,   //       Report Size (8 bits)
                    0x95, 0x01,   //       Report Count (1)
                    0xB1, 0x02,   //       Feature (Data,Var,Abs) - 1st byte of report data
                    
                    0x09, 0x32,   //       Usage (Frequency) - 0x32 from UpsUsage.cs
                    0x15, 0x00,   //       Logical Minimum (0)
                    0x26, 0xFF, 0x00, //   Logical Maximum (255)
                    0x75, 0x08,   //       Report Size (8 bits)
                    0x95, 0x01,   //       Report Count (1)
                    0xB1, 0x02,   //       Feature (Data,Var,Abs) - 2nd byte
                0xC0,             //     End Collection (Input)

                // --- Nested Output Collection within PowerConverter ---
                0x09, 0x1C,       //     Usage (Output Collection) - 0x1C from UpsUsage.cs
                0xA1, 0x02,       //     Collection (Logical)
                    0x05, 0x84,   //       Usage Page (Power Device)
                    0x09, 0x30,   //       Usage (Voltage) - 0x30 from UpsUsage.cs
                    0x15, 0x00,   //       Logical Minimum (0)
                    0x26, 0xFF, 0x00, //   Logical Maximum (255)
                    0x75, 0x08,   //       Report Size (8 bits)
                    0x95, 0x01,   //       Report Count (1)
                    0xB1, 0x02,   //       Feature (Data,Var,Abs) - 3rd byte
                    
                    0x09, 0x32,   //       Usage (Frequency) - 0x32 from UpsUsage.cs
                    0x15, 0x00,   //       Logical Minimum (0)
                    0x26, 0xFF, 0x00, //   Logical Maximum (255)
                    0x75, 0x08,   //       Report Size (8 bits)
                    0x95, 0x01,   //       Report Count (1)
                    0xB1, 0x02,   //       Feature (Data,Var,Abs) - 4th byte
                0xC0,             //     End Collection (Output)
            
            0xC0,                 //   End Collection (PowerConverter)

            // --- Battery Collection (standalone, not nested) ---
            0x09, 0x12,       //   Usage (Battery Collection) - 0x12 from UpsUsage.cs
            0xA1, 0x02,       //   Collection (Logical)
                // Battery Voltage
                0x05, 0x84,   //     Usage Page (Power Device)
                0x09, 0x30,   //     Usage (Voltage) - 0x30 from UpsUsage.cs
                0x15, 0x00,   //     Logical Minimum (0)
                0x26, 0xFF, 0x00, // Logical Maximum (255)
                0x75, 0x08,   //     Report Size (8 bits)
                0x95, 0x01,   //     Report Count (1)
                0xB1, 0x02,   //     Feature (Data,Var,Abs) - 5th byte
            0xC0,             //   End Collection (Battery)

            0xC0              // End Collection (Application)
        };

        // Test descriptor with PresentStatus bitfield in PowerSummary collection
        // This mimics the Tripplite structure where flags are in a nested PresentStatus collection
        private static readonly byte[] _presentStatusReportDescriptor = new byte[]
        {
            0x05, 0x84,       // Usage Page (Power Device)
            0x09, 0x04,       // Usage (UPS)
            0xA1, 0x01,       // Collection (Application)
            0x85, 0x02,       //   Report ID (2)

            // --- PowerSummary Collection ---
            0x09, 0x24,       //   Usage (PowerSummary) - 0x24 from docs
            0xA1, 0x02,       //   Collection (Logical)
            
                // --- PresentStatus Collection (bitfield) ---
                0x09, 0x02,       //     Usage (PresentStatus) - 0x02
                0xA1, 0x02,       //     Collection (Logical)
                    0x05, 0x85,   //       Usage Page (Battery System)
                    0x09, 0x44,   //       Usage (Charging) - 0x44
                    0x09, 0x45,   //       Usage (Discharging) - 0x45
                    0x09, 0x46,   //       Usage (FullyCharged) - 0x46
                    0x09, 0x4B,   //       Usage (NeedReplacement) - 0x4B
                    0x09, 0xD0,   //       Usage (AcPresent) - 0xD0
                    0x05, 0x84,   //       Usage Page (Power Device)
                    0x09, 0x69,   //       Usage (ShutdownImminent) - 0x69
                    0x05, 0x85,   //       Usage Page (Battery System)
                    0x09, 0x42,   //       Usage (BelowRemainingCapacityLimit) - 0x42
                    0x09, 0x47,   //       Usage (FullyDischarged) - 0x47
                    0x15, 0x00,   //       Logical Minimum (0)
                    0x25, 0x01,   //       Logical Maximum (1)
                    0x75, 0x01,   //       Report Size (1 bit)
                    0x95, 0x08,   //       Report Count (8 bits)
                    0xB1, 0x02,   //       Feature (Data,Var,Abs) - 1st byte (bitfield)
                0xC0,             //     End Collection (PresentStatus)
                
                // RemainingCapacity in PowerSummary
                0x05, 0x85,       //     Usage Page (Battery System)
                0x09, 0x66,       //     Usage (RemainingCapacity) - 0x66
                0x15, 0x00,       //     Logical Minimum (0)
                0x25, 0x64,       //     Logical Maximum (100)
                0x75, 0x08,       //     Report Size (8 bits)
                0x95, 0x01,       //     Report Count (1)
                0xB1, 0x02,       //     Feature (Data,Var,Abs) - 2nd byte
                
            0xC0,                 //   End Collection (PowerSummary)

            0xC0              // End Collection (Application)
        };

        // Deep nesting test descriptor
        private static readonly byte[] _deeplyNestedReportDescriptor = new byte[]
        {
            0x05, 0x84,       // Usage Page (Power Device)
            0x09, 0x04,       // Usage (UPS)
            0xA1, 0x01,       // Collection (Application)
            0x85, 0x03,       //   Report ID (3)

            // --- Level 1: OutletSystem Collection ---
            0x09, 0x18,       //   Usage (OutletSystem)
            0xA1, 0x02,       //   Collection (Logical)

                // --- Level 2: Outlet Collection ---
                0x09, 0x20,   //     Usage (Outlet)
                0xA1, 0x02,   //     Collection (Logical)

                    // --- Level 3: Flow Collection ---
                    0x09, 0x1E,   //       Usage (Flow)
                    0xA1, 0x00,   //       Collection (Physical)

                        // DelayBeforeShutdown
                        0x05, 0x84,   //         Usage Page (Power Device)
                        0x09, 0x57,   //         Usage (DelayBeforeShutdown)
                        0x15, 0x00,   //         Logical Minimum (0)
                        0x27, 0xFF, 0xFF, 0x00, 0x00, // Logical Maximum (65535)
                        0x75, 0x10,   //         Report Size (16 bits)
                        0x95, 0x01,   //         Report Count (1)
                        0xB1, 0x02,   //         Feature (Data,Var,Abs)

                    0xC0,         //       End Collection (Flow)
                0xC0,             //     End Collection (Outlet)
            0xC0,                 //   End Collection (OutletSystem)
            0xC0                  // End Collection (Application)
        };

        private GenericUps CreateTestUps(byte[] featureReportDescriptor, byte[] featureReportData, byte reportId = 1)
        {
            var mockDevice = new Mock<IHidDevice>();
            var mockStream = new Mock<IHidStream>();

            var descriptor = new ReportDescriptor(featureReportDescriptor);

            mockDevice.Setup(d => d.GetReportDescriptor()).Returns(descriptor);
            mockDevice.Setup(d => d.DevicePath).Returns("/test/device");
            mockDevice.Setup(d => d.GetManufacturer()).Returns("Test Manufacturer");
            mockDevice.Setup(d => d.GetProductName()).Returns("Test UPS");

            mockStream.Setup(s => s.GetFeature(It.IsAny<byte[]>()))
                .Callback((byte[] buffer) => {
                    if (buffer.Length > 0 && featureReportData.Length > 0)
                    {
                        // Keep the Report ID that was set
                        if (buffer[0] == reportId)
                        {
                            Array.Copy(featureReportData, 0, buffer, 1, Math.Min(featureReportData.Length, buffer.Length - 1));
                        }
                    }
                });

            var stream = mockStream.Object;
            mockDevice.Setup(d => d.TryOpen(out stream)).Returns(true);

            var ups = new GenericUps(mockDevice.Object);
            return ups;
        }

        [Fact]
        public void CorrectlyDistinguishesVoltagesByNestedCollections()
        {
            // Arrange: Report data with different voltage values
            // PowerConverter→Input: Voltage=120, Freq=60
            // PowerConverter→Output: Voltage=115, Freq=60
            // Battery: Voltage=24
            var reportData = new byte[] { 120, 60, 115, 60, 24 };
            var ups = CreateTestUps(_nestedCollectionReportDescriptor, reportData);

            // Act - These should use the nested collection paths
            var inputVoltage = ups.InputVoltage;
            var inputFrequency = ups.InputFrequency;
            var outputVoltage = ups.OutputVoltage;
            var outputFrequency = ups.OutputFrequency;
            var batteryVoltage = ups.BatteryVoltage;

            // Assert
            Assert.Equal(120, inputVoltage);
            Assert.Equal(60, inputFrequency);
            Assert.Equal(115, outputVoltage);
            Assert.Equal(60, outputFrequency);
            Assert.Equal(24, batteryVoltage);
        }

        [Fact]
        public void CanAccessValuesByExplicitCollectionPath()
        {
            // Arrange
            var reportData = new byte[] { 120, 60, 115, 60, 24 };
            var ups = CreateTestUps(_nestedCollectionReportDescriptor, reportData);

            // Act - Access using explicit collection paths
            var inputVoltageExplicit = ups.GetValue(
                new CollectionPath(UpsUsage.PowerConverterCollection, UpsUsage.InputCollection), 
                UpsUsage.Voltage);
            var outputVoltageExplicit = ups.GetValue(
                new CollectionPath(UpsUsage.PowerConverterCollection, UpsUsage.OutputCollection), 
                UpsUsage.Voltage);
            var batteryVoltageExplicit = ups.GetValue(
                new CollectionPath(UpsUsage.BatteryCollection), 
                UpsUsage.Voltage);

            // Assert
            Assert.Equal(120, inputVoltageExplicit);
            Assert.Equal(115, outputVoltageExplicit);
            Assert.Equal(24, batteryVoltageExplicit);
        }

        [Theory]
        [InlineData(0b00000001, false, false, false, false, true, false)]   // Bit 0: Charging
        [InlineData(0b00000010, false, true, false, false, false, false)]   // Bit 1: Discharging
        [InlineData(0b00000100, false, false, true, false, false, false)]   // Bit 2: FullyCharged
        [InlineData(0b00001000, false, false, false, true, false, false)]   // Bit 3: NeedReplacement
        [InlineData(0b00010000, false, false, false, false, false, false)]  // Bit 4: AcPresent (false = on battery)
        [InlineData(0b00100000, false, false, false, false, false, true)]   // Bit 5: ShutdownImminent
        [InlineData(0b00010101, false, false, true, false, true, false)]    // Multiple bits set
        [InlineData(0b00000000, true, false, false, false, false, false)]   // No AC = on battery
        public void PresentStatusBitfield_ParsesCorrectly(
            byte statusByte, 
            bool expectedOnBattery,
            bool expectedDischarging,
            bool expectedFullyCharged,
            bool expectedNeedReplacement,
            bool expectedCharging,
            bool expectedShutdownImminent)
        {
            // Arrange
            var reportData = new byte[] { statusByte, 75 }; // Status byte + 75% capacity
            var ups = CreateTestUps(_presentStatusReportDescriptor, reportData, 2);

            // Act
            var isOnBattery = ups.IsOnBattery;
            var isDischarging = ups.IsDischarging;
            var isFullyCharged = ups.IsFullyCharged;
            var needsReplacement = ups.NeedsReplacement;
            var isCharging = ups.IsCharging;
            var isShutdownImminent = ups.IsShutdownImminent;

            // Assert
            Assert.Equal(expectedOnBattery, isOnBattery);
            Assert.Equal(expectedDischarging, isDischarging);
            Assert.Equal(expectedFullyCharged, isFullyCharged);
            Assert.Equal(expectedNeedReplacement, needsReplacement);
            Assert.Equal(expectedCharging, isCharging);
            Assert.Equal(expectedShutdownImminent, isShutdownImminent);
        }

        [Fact]
        public void RemainingCapacity_InPowerSummaryCollection_ReadsCorrectly()
        {
            // Arrange
            var reportData = new byte[] { 0b00010100, 85 }; // AC present, fully charged flags + 85% capacity
            var ups = CreateTestUps(_presentStatusReportDescriptor, reportData, 2);

            // Act
            var remainingCapacity = ups.RemainingCapacityPercent;

            // Assert
            Assert.Equal(85, remainingCapacity);
        }

        [Fact]
        public void GetFlag_WithExplicitCollectionPath_Works()
        {
            // Arrange
            var reportData = new byte[] { 0b00000001, 50 }; // Charging bit set
            var ups = CreateTestUps(_presentStatusReportDescriptor, reportData, 2);

            // Act - Access flag using explicit collection path
            var isCharging = ups.GetFlag(
                new CollectionPath(UpsUsage.PowerSummaryCollection, UpsUsage.PresentStatus), 
                UpsUsage.Charging);

            // Assert
            Assert.True(isCharging);
        }

        [Fact]
        public void CollectionPath_Equality_Works()
        {
            // Arrange
            var path1 = new CollectionPath(UpsUsage.PowerConverterCollection, UpsUsage.InputCollection);
            var path2 = new CollectionPath(UpsUsage.PowerConverterCollection, UpsUsage.InputCollection);
            var path3 = new CollectionPath(UpsUsage.PowerConverterCollection, UpsUsage.OutputCollection);
            var path4 = new CollectionPath(UpsUsage.PowerConverterCollection);
            var emptyPath = new CollectionPath();

            // Act & Assert
            Assert.Equal(path1, path2);
            Assert.NotEqual(path1, path3);
            Assert.NotEqual(path1, path4);
            Assert.NotEqual(path4, emptyPath);
            Assert.Equal(path1.GetHashCode(), path2.GetHashCode());
        }

        [Fact]
        public void PartialPathMatching_FallsBackCorrectly()
        {
            // This tests that if we can't find an exact path match,
            // we fall back to partial matches correctly
            var reportData = new byte[] { 120, 60, 115, 60, 24 };
            var ups = CreateTestUps(_nestedCollectionReportDescriptor, reportData);

            // Act - Try to get battery voltage with just Battery collection (not the full path)
            var batteryVoltage = ups.GetValue(
                new CollectionPath(UpsUsage.BatteryCollection), 
                UpsUsage.Voltage);

            // Assert - Should still find it
            Assert.Equal(24, batteryVoltage);
        }

        [Fact]
        public void ToString_ProducesReadablePath()
        {
            // Arrange
            var simplePath = new CollectionPath(UpsUsage.BatteryCollection);
            var nestedPath = new CollectionPath(UpsUsage.PowerConverterCollection, UpsUsage.InputCollection);

            // Act
            var simpleStr = simplePath.ToString();
            var nestedStr = nestedPath.ToString();

            // Assert
            Assert.Equal("BatteryCollection", simpleStr);
            Assert.Equal("PowerConverterCollection → InputCollection", nestedStr);
        }

        // Test the internal helper for nested collections
        private class TestableGenericUps : GenericUps
        {
            public TestableGenericUps(IHidDevice device) : base(device) { }

            public bool SetTestValue(CollectionPath path, UpsUsage usage, int value)
            {
                return SetValue(path, usage, value);
            }
        }

        [Fact]
        public void SetValue_WithNestedCollectionPath_WritesToCorrectLocation()
        {
            // Arrange
            var mockDevice = new Mock<IHidDevice>();
            var mockStream = new Mock<IHidStream>();
            var descriptor = new ReportDescriptor(_nestedCollectionReportDescriptor);

            mockDevice.Setup(d => d.GetReportDescriptor()).Returns(descriptor);
            mockDevice.Setup(d => d.DevicePath).Returns("/test/device");
            mockDevice.Setup(d => d.GetManufacturer()).Returns("Test Manufacturer");
            mockDevice.Setup(d => d.GetProductName()).Returns("Test UPS");

            byte[] writtenData = null;
            mockStream.Setup(s => s.SetFeature(It.IsAny<byte[]>()))
                .Callback((byte[] buffer) => {
                    writtenData = new byte[buffer.Length];
                    Array.Copy(buffer, writtenData, buffer.Length);
                });

            var stream = mockStream.Object;
            mockDevice.Setup(d => d.TryOpen(out stream)).Returns(true);

            var ups = new TestableGenericUps(mockDevice.Object);

            // Act - Try to set the output voltage (PowerConverter → Output → Voltage)
            bool success = ups.SetTestValue(
                new CollectionPath(UpsUsage.PowerConverterCollection, UpsUsage.OutputCollection),
                UpsUsage.Voltage, 
                230);

            // Assert
            Assert.True(success);
            Assert.NotNull(writtenData);
            Assert.True(writtenData.Length >= 4); // Report ID + at least 3 bytes to reach output voltage
            Assert.Equal(1, writtenData[0]);      // Report ID
            Assert.Equal(230, writtenData[3]);    // Output voltage is the 3rd data byte (index 3 after report ID)
        }

        [Fact]
        public void GetValue_NonExistentUsage_ReturnsNull()
        {
            // Arrange
            var reportData = new byte[] { 120, 60, 115, 60, 24 };
            var ups = CreateTestUps(_nestedCollectionReportDescriptor, reportData);

            // Act - Try to get a usage that doesn't exist
            var nonExistent = ups.GetValue(
                new CollectionPath(UpsUsage.PowerConverterCollection),
                UpsUsage.Temperature); // Temperature isn't in this descriptor

            // Assert
            Assert.Null(nonExistent);
        }

        [Fact]
        public void GetFlag_NonExistentFlag_ReturnsNull()
        {
            // Arrange
            var reportData = new byte[] { 0b00000001, 50 };
            var ups = CreateTestUps(_presentStatusReportDescriptor, reportData, 2);

            // Act - Try to get a flag that doesn't exist in this descriptor
            var overloaded = ups.IsOverloaded; // Overload flag isn't in this descriptor

            // Assert
            Assert.Null(overloaded);
        }
    }
}