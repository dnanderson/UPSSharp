using HidSharp;
using HidSharp.Reports;
using System.Collections.Generic;
using System.Linq;

namespace HidUps
{
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
            var reports = _reportDescriptor.FeatureReports.Concat(_reportDescriptor.InputReports);

            foreach (var report in reports)
            {
                int currentBitOffset = 8; // Start after the 8-bit Report ID
                foreach (var dataItem in report.DataItems)
                {
                    var usages = dataItem.Usages.GetAllValues().ToArray();

                    // If the item itself is a bitfield collection, check its children usages
                    if (dataItem.ElementBits == 1 && dataItem.ElementCount > 1 && usages.Length >= dataItem.ElementCount)
                    {
                        for (int i = 0; i < dataItem.ElementCount; i++)
                        {
                            var usage = (UpsUsage)usages[i];
                            if (!_usageMap.ContainsKey(usage))
                            {
                                _usageMap[usage] = new MappedUsage(report, dataItem, currentBitOffset, i);
                            }
                        }
                    }
                    // Handle scalar values
                    else if (usages.Length > 0)
                    {
                        var usage = (UpsUsage)usages[0];
                        if (!_usageMap.ContainsKey(usage))
                        {
                            _usageMap[usage] = new MappedUsage(report, dataItem, currentBitOffset);
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
        public bool IsOnBattery => GetFlag(UpsUsage.AcPresent) == false;
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
            if (!_usageMap.TryGetValue(usage, out var mappedUsage) || !mappedUsage.BitIndex.HasValue) { return null; }

            var buffer = GetReport(mappedUsage);
            if (buffer == null) { return null; }

            uint rawValue = mappedUsage.DataItem.ReadRaw(buffer, mappedUsage.DataItemBitOffset, mappedUsage.BitIndex.Value);
            return rawValue == 1;
        }

        #endregion
    }
}
