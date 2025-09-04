using HidSharp;
using HidSharp.Reports;
using System.Collections.Generic;
using System.Linq;

namespace HidUps
{
    /// <summary>
    /// Represents a command to be sent to the UPS Test usage.
    /// </summary>
    public enum UpsTestCommand
    {
        QuickTest = 1
    }

    /// <summary>
    /// Represents the result of a UPS test.
    /// </summary>
    public enum UpsTestResult
    {
        NoTestInitiated = 0,
        DoneAndPassed = 1,
        DoneAndWarning = 2,
        DoneAndError = 3,
        Aborted = 4,
        InProgress = 5
    }


    /// <summary>
    /// Represents a generic, model-agnostic HID UPS device.
    /// It discovers features at runtime by parsing the HID Report Descriptor.
    /// </summary>
    public class GenericUps
    {
        private readonly HidDevice _device;
        private readonly ReportDescriptor _reportDescriptor;
        private readonly Dictionary<UpsUsage, MappedUsage> _usageMap;

        /// <summary>
        /// A helper class to store information about a discovered HID usage.
        /// </summary>
        private class MappedUsage
        {
            public Report Report { get; }
            public DataItem DataItem { get; }
            public int DataItemBitOffset { get; }
            public int? BitIndex { get; } // Used for flags within a bitfield

            public MappedUsage(Report report, DataItem dataItem, int dataItemBitOffset, int? bitIndex = null)
            {
                Report = report;
                DataItem = dataItem;
                DataItemBitOffset = dataItemBitOffset;
                BitIndex = bitIndex;
            }
        }

        private GenericUps(HidDevice device)
        {
            _device = device;
            _reportDescriptor = device.GetReportDescriptor();
            _usageMap = new Dictionary<UpsUsage, MappedUsage>();
            ParseAndMapUsages();
        }

        /// <summary>
        /// Finds all connected HID UPS devices.
        /// </summary>
        /// <returns>An enumerable of initialized GenericUps objects.</returns>
        public static IEnumerable<GenericUps> FindAll()
        {
            var powerDeviceUsagePage = (uint)UpsUsage.PowerDevicePage >> 16;
            var upsUsage = (uint)UpsUsage.Ups;
            var powerSupplyUsage = (uint)UpsUsage.PowerSupply;

            var devices = DeviceList.Local.GetHidDevices()
                .Where(d =>
                {
                    try
                    {
                        var reportDescriptor = d.GetReportDescriptor();
                        return reportDescriptor.DeviceItems.Any(item =>
                            item.Usages.GetAllValues().Any(usage =>
                                ((usage >> 16) == powerDeviceUsagePage) && (usage == upsUsage || usage == powerSupplyUsage)
                            )
                        );
                    }
                    catch
                    {
                        // Can't access descriptor, ignore device.
                        return false;
                    }
                });

            foreach (var device in devices)
            {
                yield return new GenericUps(device);
            }
        }

        /// <summary>
        /// Parses the device's report descriptor to map standard UPS usages to their actual reports and data items.
        /// </summary>
        private void ParseAndMapUsages()
        {
            var reports = _reportDescriptor.FeatureReports
                                           .Concat(_reportDescriptor.InputReports)
                                           .Concat(_reportDescriptor.OutputReports);

            foreach (var report in reports)
            {
                int currentBitOffset = 8; // Start after the 8-bit Report ID
                foreach (var dataItem in report.DataItems)
                {
                    var usages = dataItem.Usages.GetAllValues().ToArray();

                    // Case 1: Variable bitfield (one bit per flag).
                    if (dataItem.IsVariable && dataItem.ElementBits == 1 && dataItem.ElementCount > 1)
                    {
                        int count = Math.Min(dataItem.ElementCount, usages.Length);
                        for (int i = 0; i < count; i++)
                        {
                            var usage = (UpsUsage)usages[i];
                            if (!_usageMap.ContainsKey(usage))
                            {
                                _usageMap[usage] = new MappedUsage(report, dataItem, currentBitOffset, i);
                            }
                        }
                    }
                    // Case 2: Scalar value or an Array of states.
                    else
                    {
                        // Map every possible usage associated with this item.
                        // For scalars, it will be one. For arrays, it will be all possible states.
                        foreach (var usageValue in usages)
                        {
                            var usage = (UpsUsage)usageValue;
                            if (!_usageMap.ContainsKey(usage))
                            {
                                _usageMap[usage] = new MappedUsage(report, dataItem, currentBitOffset);
                            }
                        }
                    }
                    currentBitOffset += dataItem.TotalBits;
                }
            }
        }

        #region Public Properties

        public string DevicePath => _device.DevicePath;
        public string Manufacturer => _device.GetManufacturer();
        public string ProductName => _device.GetProductName();
        public string SerialNumber => _device.GetSerialNumber();

        // --- Status Properties ---
        public bool? IsOnBattery => GetFlag(UpsUsage.AcPresent) == false;
        public bool? IsCharging => GetFlag(UpsUsage.Charging);
        public bool? IsDischarging => GetFlag(UpsUsage.Discharging);
        public bool? IsFullyCharged => GetFlag(UpsUsage.FullyCharged);
        public bool? NeedsReplacement => GetFlag(UpsUsage.NeedReplacement);
        public bool? IsOverloaded => GetFlag(UpsUsage.Overload);
        public bool? IsShutdownImminent => GetFlag(UpsUsage.ShutdownImminent);

        // --- Value Properties ---
        public double? RemainingCapacityPercent => GetPhysicalValue(UpsUsage.RemainingCapacity);
        public double? RunTimeToEmptySeconds => GetPhysicalValue(UpsUsage.RunTimeToEmpty);
        public double? PercentLoad => GetPhysicalValue(UpsUsage.PercentLoad);
        public double? InputVoltage => GetPhysicalValue(UpsUsage.Voltage);
        public double? InputFrequency => GetPhysicalValue(UpsUsage.Frequency);
        public double? OutputActivePowerWatts => GetPhysicalValue(UpsUsage.ActivePower);
        public double? OutputApparentPowerVA => GetPhysicalValue(UpsUsage.ApparentPower);

        #endregion

        #region Public Methods

        /// <summary>
        /// Initiates a quick self-test on the UPS.
        /// </summary>
        /// <returns>True if the command was sent successfully, otherwise false.</returns>
        public bool RunQuickTest()
        {
            return SetValue(UpsUsage.Test, (int)UpsTestCommand.QuickTest);
        }

        /// <summary>
        /// Gets the current status of a self-test.
        /// </summary>
        /// <returns>The current test result, or null if the status cannot be retrieved.</returns>
        public UpsTestResult? GetTestResult()
        {
            var value = GetPhysicalValue(UpsUsage.Test);
            return value.HasValue ? (UpsTestResult?)(int)value.Value : null;
        }

        #endregion

        #region Private Helper Methods

        private byte[] GetReport(MappedUsage mappedUsage)
        {
            if (mappedUsage.Report.ReportType != HidSharp.Reports.ReportType.Feature &&
                mappedUsage.Report.ReportType != HidSharp.Reports.ReportType.Input)
            {
                // Only Feature and Input reports are supported for reading status.
                return null;
            }

            if (!_device.TryOpen(out var stream)) { return null; }

            using (stream)
            {
                stream.ReadTimeout = 2000;
                var buffer = new byte[mappedUsage.Report.Length];
                if (mappedUsage.Report.ReportID != 0)
                {
                    buffer[0] = mappedUsage.Report.ReportID;
                }

                try
                {
                    if (mappedUsage.Report.ReportType == HidSharp.Reports.ReportType.Feature)
                    {
                        stream.GetFeature(buffer);
                    }
                    else // Input Report
                    {
                        stream.Read(buffer);
                    }
                    return buffer;
                }
                catch
                {
                    return null;
                }
            }
        }

        private bool SetValue(UpsUsage usage, int logicalValue)
        {
            if (!_usageMap.TryGetValue(usage, out var mappedUsage)) { return false; }

            var report = mappedUsage.Report;
            var dataItem = mappedUsage.DataItem;

            if (report.ReportType != HidSharp.Reports.ReportType.Feature &&
                report.ReportType != HidSharp.Reports.ReportType.Output)
            {
                // Only Feature and Output reports are writable.
                return false;
            }

            if (!_device.TryOpen(out var stream)) { return false; }

            using (stream)
            {
                stream.WriteTimeout = 2000;
                var buffer = new byte[report.Length];
                buffer[0] = report.ReportID;

                // Write our desired value into the buffer at the correct location.
                dataItem.WriteLogical(buffer, mappedUsage.DataItemBitOffset, 0, logicalValue);

                try
                {
                    if (report.ReportType == HidSharp.Reports.ReportType.Feature)
                    {
                        stream.SetFeature(buffer);
                    }
                    else // Output Report
                    {
                        stream.Write(buffer);
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }


        private double? GetPhysicalValue(UpsUsage usage)
        {
            if (!_usageMap.TryGetValue(usage, out var mappedUsage)) { return null; }

            var buffer = GetReport(mappedUsage);
            if (buffer == null) { return null; }

            var logicalValue = mappedUsage.DataItem.ReadLogical(buffer, mappedUsage.DataItemBitOffset, 0);
            return DataConvert.PhysicalFromLogical(mappedUsage.DataItem, logicalValue);
        }

        private bool? GetFlag(UpsUsage usage)
        {
            if (!_usageMap.TryGetValue(usage, out var mappedUsage)) { return null; }

            var buffer = GetReport(mappedUsage);
            if (buffer == null) { return null; }

            var dataItem = mappedUsage.DataItem;

            // Case 1: Variable bitfield. Each bit is a flag.
            if (dataItem.IsVariable && mappedUsage.BitIndex.HasValue)
            {
                if (mappedUsage.BitIndex.Value >= dataItem.ElementCount) return null;

                // The elementIndex for ReadRaw is the bit index.
                uint rawValue = dataItem.ReadRaw(buffer, mappedUsage.DataItemBitOffset, mappedUsage.BitIndex.Value);
                return rawValue == 1;
            }

            // Case 2: Array of selectors. The report contains the logical value(s) of active state(s).
            if (dataItem.IsArray)
            {
                // Find the index corresponding to our target usage in the DataItem's usage list.
                if (dataItem.Usages.TryGetIndexFromValue((uint)usage, out int targetUsageIndex))
                {
                    // The logical value reported for an array usage is its index + logical minimum.
                    int targetLogicalValue = targetUsageIndex + dataItem.LogicalMinimum;

                    // Check all elements reported by this DataItem.
                    for (int i = 0; i < dataItem.ElementCount; i++)
                    {
                        int logicalValueInReport = dataItem.ReadLogical(buffer, mappedUsage.DataItemBitOffset, i);
                        if (logicalValueInReport == targetLogicalValue)
                        {
                            return true; // Our target usage/state is active.
                        }
                    }
                }
                return false; // The flag is not active.
            }

            // Case 3: Simple boolean in a scalar value.
            if (dataItem.IsVariable && !mappedUsage.BitIndex.HasValue)
            {
                var logicalValue = dataItem.ReadLogical(buffer, mappedUsage.DataItemBitOffset, 0);
                // A simple boolean is typically 1 for true, 0 for false.
                if (logicalValue == dataItem.LogicalMaximum) return true;
                if (logicalValue == dataItem.LogicalMinimum) return false;
            }

            return null; // Not a recognized flag type or unable to parse.
        }

        #endregion
    }
}

