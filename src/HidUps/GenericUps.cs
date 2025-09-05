using HidSharp;
using HidSharp.Reports;
using System;
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
        private readonly Dictionary<(uint, UpsUsage), MappedUsage> _usageMap;

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
            _usageMap = new Dictionary<(uint, UpsUsage), MappedUsage>();
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
        /// Parses the device's report descriptor to map standard UPS usages to their actual reports and data items,
        /// respecting the collection hierarchy.
        /// </summary>
        private void ParseAndMapUsages()
        {
            // The top-level DeviceItems are the starting point of the collection hierarchy.
            foreach (var deviceItem in _reportDescriptor.DeviceItems)
            {
                ParseCollection(deviceItem, 0); // Start with a default (0) parent collection usage.
            }
        }

        /// <summary>
        /// Recursively parses a collection and its children to map usages.
        /// </summary>
        /// <param name="collection">The collection to parse.</param>
        /// <param name="parentCollectionUsage">The usage of the parent collection.</param>
        private void ParseCollection(DeviceItem collection, uint parentCollectionUsage)
        {
            // Determine the usage for the current collection. Fall back to the parent's usage if not specified.
            uint currentCollectionUsage = collection.Usages.GetAllValues().FirstOrDefault();
            if (currentCollectionUsage == 0)
            {
                currentCollectionUsage = parentCollectionUsage;
            }

            var allReports = _reportDescriptor.FeatureReports
                                           .Concat(_reportDescriptor.InputReports)
                                           .Concat(_reportDescriptor.OutputReports);

            // Process all data items that are direct children of this collection.
            foreach (var dataItem in collection.DataItems)
            {
                // Find the report this data item belongs to.
                var report = allReports.FirstOrDefault(r => r.ReportID == dataItem.ReportID);
                if (report == null) continue;

                // Calculate the bit offset of this data item within its report.
                int currentBitOffset = 8; // Start after the 8-bit Report ID.
                foreach (var di in report.DataItems)
                {
                    if (di == dataItem) break;
                    currentBitOffset += di.TotalBits;
                }

                var usages = dataItem.Usages.GetAllValues().ToArray();

                // Case 1: Variable bitfield (one bit per flag).
                if (dataItem.IsVariable && dataItem.ElementBits == 1 && dataItem.ElementCount > 1)
                {
                    int count = Math.Min(dataItem.ElementCount, usages.Length);
                    for (int i = 0; i < count; i++)
                    {
                        var usage = (UpsUsage)usages[i];
                        var key = (currentCollectionUsage, usage);
                        if (!_usageMap.ContainsKey(key))
                        {
                            _usageMap[key] = new MappedUsage(report, dataItem, currentBitOffset, i);
                        }
                    }
                }
                // Case 2: Scalar value or an Array of states.
                else
                {
                    foreach (var usageValue in usages)
                    {
                        var usage = (UpsUsage)usageValue;
                        var key = (currentCollectionUsage, usage);
                        if (!_usageMap.ContainsKey(key))
                        {
                            _usageMap[key] = new MappedUsage(report, dataItem, currentBitOffset);
                        }
                    }
                }
            }

            // Recursively parse child collections.
            foreach (var childCollection in collection.Children)
            {
                ParseCollection(childCollection, currentCollectionUsage);
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
        public double? PercentLoad => GetPhysicalValue(UpsUsage.PercentLoad, (uint)UpsUsage.OutputCollection);
        public double? InputVoltage => GetPhysicalValue(UpsUsage.Voltage, (uint)UpsUsage.InputCollection);
        public double? OutputVoltage => GetPhysicalValue(UpsUsage.Voltage, (uint)UpsUsage.OutputCollection);
        public double? BatteryVoltage => GetPhysicalValue(UpsUsage.Voltage, (uint)UpsUsage.BatteryCollection);
        public double? InputFrequency => GetPhysicalValue(UpsUsage.Frequency, (uint)UpsUsage.InputCollection);
        public double? OutputFrequency => GetPhysicalValue(UpsUsage.Frequency, (uint)UpsUsage.OutputCollection);
        public double? OutputActivePowerWatts => GetPhysicalValue(UpsUsage.ActivePower, (uint)UpsUsage.OutputCollection);
        public double? OutputApparentPowerVA => GetPhysicalValue(UpsUsage.ApparentPower, (uint)UpsUsage.OutputCollection);

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

        private bool SetValue(UpsUsage usage, int logicalValue, uint? collectionUsage = null)
        {
            MappedUsage mappedUsage;
            if (collectionUsage.HasValue)
            {
                if (!_usageMap.TryGetValue((collectionUsage.Value, usage), out mappedUsage)) { return false; }
            }
            else
            {
                // Find first match for the usage, regardless of collection.
                var mapEntry = _usageMap.FirstOrDefault(kvp => kvp.Key.Item2 == usage);
                if (mapEntry.Value == null) { return false; }
                mappedUsage = mapEntry.Value;
            }

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


        private double? GetPhysicalValue(UpsUsage usage, uint? collectionUsage = null)
        {
            MappedUsage mappedUsage;
            if (collectionUsage.HasValue)
            {
                if (!_usageMap.TryGetValue((collectionUsage.Value, usage), out mappedUsage)) { return null; }
            }
            else
            {
                // Find first match for the usage, regardless of collection.
                var mapEntry = _usageMap.FirstOrDefault(kvp => kvp.Key.Item2 == usage);
                if (mapEntry.Value == null) { return null; }
                mappedUsage = mapEntry.Value;
            }

            var buffer = GetReport(mappedUsage);
            if (buffer == null) { return null; }

            var dataItem = mappedUsage.DataItem;
            var logicalValue = dataItem.ReadLogical(buffer, mappedUsage.DataItemBitOffset, 0);

            if (DataConvert.IsLogicalOutOfRange(dataItem, logicalValue))
            {
                return null;
            }

            return DataConvert.PhysicalFromLogical(dataItem, logicalValue);
        }

        private bool? GetFlag(UpsUsage usage, uint? collectionUsage = null)
        {
            MappedUsage mappedUsage;
            if (collectionUsage.HasValue)
            {
                if (!_usageMap.TryGetValue((collectionUsage.Value, usage), out mappedUsage)) { return null; }
            }
            else
            {
                // Find first match for the usage, regardless of collection.
                var mapEntry = _usageMap.FirstOrDefault(kvp => kvp.Key.Item2 == usage);
                if (mapEntry.Value == null) { return null; }
                mappedUsage = mapEntry.Value;
            }

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

