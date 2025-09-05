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
        private readonly IHidDevice _device;
        private readonly ReportDescriptor _reportDescriptor;
        private readonly Dictionary<(UpsUsage, UpsUsage), MappedUsage> _usageMap;

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

        internal GenericUps(IHidDevice device)
        {
            _device = device;
            _reportDescriptor = device.GetReportDescriptor();
            _usageMap = new Dictionary<(UpsUsage, UpsUsage), MappedUsage>();
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
                yield return new GenericUps(new HidDeviceWrapper(device));
            }
        }

        private UpsUsage GetCollectionUsage(DataItem dataItem)
        {
            var parent = dataItem.ParentItem;
            var collectionUsages = new List<UpsUsage>();
            
            while (parent != null)
            {
                if (parent is DescriptorCollectionItem collectionItem)
                {
                    // Get the usage of this collection
                    var usage = (UpsUsage)collectionItem.Usages.GetAllValues().FirstOrDefault();
                    if (usage != 0)
                    {
                        collectionUsages.Add(usage);
                        
                        // Check if this is a recognized collection type
                        if (IsRecognizedCollection(usage))
                        {
                            return usage;
                        }
                    }
                }
                parent = parent.ParentItem;
            }
            
            // If no recognized collection found, check if we're in PowerSummary
            // Many Tripplite usages are directly in PowerSummary
            if (collectionUsages.Any(u => u == UpsUsage.PowerSummaryCollection))
            {
                return UpsUsage.PowerSummaryCollection;
            }
            
            return 0; // Root/no collection
        }
        
        private bool IsRecognizedCollection(UpsUsage usage)
        {
            return usage == UpsUsage.InputCollection ||
                   usage == UpsUsage.OutputCollection ||
                   usage == UpsUsage.BatteryCollection ||
                   usage == UpsUsage.PowerSummaryCollection ||
                   usage == UpsUsage.BatterySystemCollection ||
                   usage == UpsUsage.FlowCollection ||
                   usage == UpsUsage.PowerConverterCollection ||
                   usage == UpsUsage.OutletSystemCollection;
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
                int currentBitOffset = _reportDescriptor.ReportsUseID ? 8 : 0; // Start after the Report ID if it exists
                foreach (var dataItem in report.DataItems)
                {
                    var collectionUsage = GetCollectionUsage(dataItem);
                    var usages = dataItem.Usages.GetAllValues().ToArray();

                    // Case 1: Variable bitfield (one bit per flag).
                    if (dataItem.IsVariable && dataItem.ElementBits == 1 && dataItem.ElementCount > 1)
                    {
                        int count = Math.Min(dataItem.ElementCount, usages.Length);
                        for (int i = 0; i < count; i++)
                        {
                            var usage = (UpsUsage)usages[i];
                            RegisterUsageMapping(collectionUsage, usage, report, dataItem, currentBitOffset, i);
                        }
                    }
                    // Case 2: Scalar value or an Array of states.
                    else
                    {
                        // Map every possible usage associated with this item.
                        foreach (var usageValue in usages)
                        {
                            var usage = (UpsUsage)usageValue;
                            RegisterUsageMapping(collectionUsage, usage, report, dataItem, currentBitOffset);
                        }
                    }
                    currentBitOffset += dataItem.TotalBits;
                }
            }
        }
        
        private void RegisterUsageMapping(UpsUsage collection, UpsUsage usage, Report report, 
            DataItem dataItem, int bitOffset, int? bitIndex = null)
        {
            // Register with collection context
            var key = (collection, usage);
            if (!_usageMap.ContainsKey(key))
            {
                _usageMap[key] = new MappedUsage(report, dataItem, bitOffset, bitIndex);
            }
            
            // Also register without collection context for backwards compatibility
            // and for cases where the collection might not be properly identified
            var rootKey = ((UpsUsage)0, usage);
            if (!_usageMap.ContainsKey(rootKey))
            {
                _usageMap[rootKey] = new MappedUsage(report, dataItem, bitOffset, bitIndex);
            }
        }

        #region Public Properties

        public string DevicePath => _device.DevicePath;
        public string Manufacturer => _device.GetManufacturer();
        public string ProductName => _device.GetProductName();
        public string SerialNumber => _device.GetSerialNumber();

        // --- Status Properties ---
        // Try multiple possible locations for these values based on Tripplite protocol
        
        // AC Present can be in root or PowerSummary's PresentStatus
        public bool? IsOnBattery
        {
            get
            {
                // First try the standalone ACPresent flag
                var acPresent = GetFlag(UpsUsage.AcPresent);
                if (acPresent.HasValue) return !acPresent.Value;
                
                // Try in PowerSummary collection
                acPresent = GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.AcPresent);
                if (acPresent.HasValue) return !acPresent.Value;
                
                return null;
            }
        }
        
        public bool? IsShutdownImminent
        {
            get
            {
                // Try PowerSummary first (Tripplite location)
                var result = GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.ShutdownImminent);
                if (result.HasValue) return result;
                return GetFlag(UpsUsage.ShutdownImminent);
            }
        }

        // Battery Status - Try both BatterySystem and PowerSummary collections
        public bool? IsCharging
        {
            get
            {
                // Try BatterySystem collection first
                var result = GetFlag(UpsUsage.BatterySystemCollection, UpsUsage.Charging);
                if (result.HasValue) return result;
                
                // Try PowerSummary (Tripplite PresentStatus)
                result = GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.Charging);
                if (result.HasValue) return result;
                
                // Try generic Battery collection
                return GetFlag(UpsUsage.BatteryCollection, UpsUsage.Charging);
            }
        }
        
        public bool? IsDischarging
        {
            get
            {
                var result = GetFlag(UpsUsage.BatterySystemCollection, UpsUsage.Discharging);
                if (result.HasValue) return result;
                
                result = GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.Discharging);
                if (result.HasValue) return result;
                
                return GetFlag(UpsUsage.BatteryCollection, UpsUsage.Discharging);
            }
        }
        
        public bool? IsFullyCharged
        {
            get
            {
                var result = GetFlag(UpsUsage.BatterySystemCollection, UpsUsage.FullyCharged);
                if (result.HasValue) return result;
                
                result = GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.FullyCharged);
                if (result.HasValue) return result;
                
                return GetFlag(UpsUsage.BatteryCollection, UpsUsage.FullyCharged);
            }
        }
        
        public bool? NeedsReplacement
        {
            get
            {
                var result = GetFlag(UpsUsage.BatterySystemCollection, UpsUsage.NeedReplacement);
                if (result.HasValue) return result;
                
                result = GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.NeedReplacement);
                if (result.HasValue) return result;
                
                return GetFlag(UpsUsage.BatteryCollection, UpsUsage.NeedReplacement);
            }
        }

        // Output Status - Try PowerConverter collection (Tripplite location)
        public bool? IsOverloaded
        {
            get
            {
                var result = GetFlag(UpsUsage.PowerConverterCollection, UpsUsage.Overload);
                if (result.HasValue) return result;
                
                return GetFlag(UpsUsage.OutputCollection, UpsUsage.Overload);
            }
        }

        // --- Value Properties ---
        // Battery Values - Try multiple collections
        public double? RemainingCapacityPercent
        {
            get
            {
                // Try PowerSummary first (Tripplite location)
                var result = GetPhysicalValue(UpsUsage.PowerSummaryCollection, UpsUsage.RemainingCapacity);
                if (result.HasValue) return result;
                
                // Try BatterySystem
                result = GetPhysicalValue(UpsUsage.BatterySystemCollection, UpsUsage.RemainingCapacity);
                if (result.HasValue) return result;
                
                return GetPhysicalValue(UpsUsage.BatteryCollection, UpsUsage.RemainingCapacity);
            }
        }
        
        public double? RunTimeToEmptySeconds
        {
            get
            {
                // Try PowerSummary first (Tripplite location)
                var result = GetPhysicalValue(UpsUsage.PowerSummaryCollection, UpsUsage.RunTimeToEmpty);
                if (result.HasValue) return result;
                
                // Try BatterySystem
                result = GetPhysicalValue(UpsUsage.BatterySystemCollection, UpsUsage.RunTimeToEmpty);
                if (result.HasValue) return result;
                
                return GetPhysicalValue(UpsUsage.BatteryCollection, UpsUsage.RunTimeToEmpty);
            }
        }
        
        public double? BatteryVoltage
        {
            get
            {
                var result = GetPhysicalValue(UpsUsage.BatterySystemCollection, UpsUsage.Voltage);
                if (result.HasValue) return result;
                
                return GetPhysicalValue(UpsUsage.BatteryCollection, UpsUsage.Voltage);
            }
        }

        // Input Values - Try PowerConverter collection first (Tripplite)
        public double? InputVoltage
        {
            get
            {
                var result = GetPhysicalValue(UpsUsage.PowerConverterCollection, UpsUsage.Voltage);
                if (result.HasValue) return result;
                
                result = GetPhysicalValue(UpsUsage.InputCollection, UpsUsage.Voltage);
                if (result.HasValue) return result;
                
                // Try PowerSummary as well
                return GetPhysicalValue(UpsUsage.PowerSummaryCollection, UpsUsage.Voltage);
            }
        }
        
        public double? InputFrequency
        {
            get
            {
                var result = GetPhysicalValue(UpsUsage.PowerConverterCollection, UpsUsage.Frequency);
                if (result.HasValue) return result;
                
                return GetPhysicalValue(UpsUsage.InputCollection, UpsUsage.Frequency);
            }
        }

        // Output Values
        public double? OutputVoltage => GetPhysicalValue(UpsUsage.OutputCollection, UpsUsage.Voltage);
        
        public double? PercentLoad
        {
            get
            {
                // Try OutletSystem first (Tripplite location)
                var result = GetPhysicalValue(UpsUsage.OutletSystemCollection, UpsUsage.PercentLoad);
                if (result.HasValue) return result;
                
                return GetPhysicalValue(UpsUsage.OutputCollection, UpsUsage.PercentLoad);
            }
        }
        
        public double? OutputActivePowerWatts
        {
            get
            {
                // Try OutletSystem first (Tripplite)
                var result = GetPhysicalValue(UpsUsage.OutletSystemCollection, UpsUsage.ActivePower);
                if (result.HasValue && result.Value != 0xFFFF) return result;
                
                result = GetPhysicalValue(UpsUsage.PowerConverterCollection, UpsUsage.ActivePower);
                if (result.HasValue && result.Value != 0xFFFF) return result;
                
                return GetPhysicalValue(UpsUsage.OutputCollection, UpsUsage.ActivePower);
            }
        }
        
        public double? OutputApparentPowerVA => GetPhysicalValue(UpsUsage.OutputCollection, UpsUsage.ApparentPower);

        // Add a public method for debugging
        public void DumpMappings()
        {
            Console.WriteLine("\n=== Usage Mappings ===");
            
            // Check for specific important usages
            var checkUsages = new[] {
                (UpsUsage.PowerSummaryCollection, UpsUsage.RemainingCapacity, "PowerSummary/RemainingCapacity"),
                ((UpsUsage)0, UpsUsage.RemainingCapacity, "Root/RemainingCapacity"),
                (UpsUsage.BatterySystemCollection, UpsUsage.RemainingCapacity, "BatterySystem/RemainingCapacity"),
                (UpsUsage.PowerSummaryCollection, UpsUsage.RunTimeToEmpty, "PowerSummary/RunTimeToEmpty"),
                ((UpsUsage)0, UpsUsage.RunTimeToEmpty, "Root/RunTimeToEmpty"),
                (UpsUsage.PowerSummaryCollection, UpsUsage.PresentStatus, "PowerSummary/PresentStatus"),
                ((UpsUsage)0, UpsUsage.PresentStatus, "Root/PresentStatus"),
                (UpsUsage.PowerSummaryCollection, UpsUsage.FullyCharged, "PowerSummary/FullyCharged"),
                ((UpsUsage)0, UpsUsage.FullyCharged, "Root/FullyCharged"),
            };
            
            foreach (var (collection, usage, name) in checkUsages)
            {
                if (_usageMap.TryGetValue((collection, usage), out var mapped))
                {
                    Console.WriteLine($"  ✓ {name}: Report {mapped.Report.ReportID}, Type: {mapped.Report.ReportType}, BitOffset: {mapped.DataItemBitOffset}, BitIndex: {mapped.BitIndex}");
                }
                else
                {
                    Console.WriteLine($"  ✗ {name}: NOT MAPPED");
                }
            }
            
            Console.WriteLine("\nAll mapped usages by Report ID:");
            var byReport = _usageMap.GroupBy(kvp => kvp.Value.Report.ReportID).OrderBy(g => g.Key);
            foreach (var reportGroup in byReport)
            {
                Console.WriteLine($"  Report {reportGroup.Key}:");
                foreach (var kvp in reportGroup.Take(5)) // Limit output
                {
                    var collectionName = kvp.Key.Item1 == 0 ? "Root" : kvp.Key.Item1.ToString();
                    var usageName = kvp.Key.Item2.ToString();
                    Console.WriteLine($"    {collectionName}/{usageName}");
                }
                if (reportGroup.Count() > 5)
                    Console.WriteLine($"    ... and {reportGroup.Count() - 5} more");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initiates a quick self-test on the UPS.
        /// </summary>
        /// <returns>True if the command was sent successfully, otherwise false.</returns>
        public bool RunQuickTest()
        {
            // Try BatterySystem collection first (Tripplite location)
            if (SetValue(UpsUsage.BatterySystemCollection, UpsUsage.Test, (int)UpsTestCommand.QuickTest))
                return true;
                
            return SetValue(UpsUsage.Test, (int)UpsTestCommand.QuickTest);
        }

        /// <summary>
        /// Gets the current status of a self-test.
        /// </summary>
        /// <returns>The current test result, or null if the status cannot be retrieved.</returns>
        public UpsTestResult? GetTestResult()
        {
            // Try BatterySystem collection first (Tripplite location)
            var value = GetPhysicalValue(UpsUsage.BatterySystemCollection, UpsUsage.Test);
            if (value.HasValue) return (UpsTestResult?)(int)value.Value;
            
            value = GetPhysicalValue(UpsUsage.Test);
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

            if (!_device.TryOpen(out IHidStream stream)) { return null; }

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

        protected bool SetValue(UpsUsage usage, int logicalValue) => SetValue(0, usage, logicalValue);

        protected bool SetValue(UpsUsage collection, UpsUsage usage, int logicalValue)
        {
            if (!_usageMap.TryGetValue((collection, usage), out var mappedUsage)) { return false; }

            var report = mappedUsage.Report;
            var dataItem = mappedUsage.DataItem;

            if (report.ReportType != HidSharp.Reports.ReportType.Feature &&
                report.ReportType != HidSharp.Reports.ReportType.Output)
            {
                // Only Feature and Output reports are writable.
                return false;
            }

            if (!_device.TryOpen(out IHidStream stream)) { return false; }

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

        private double? GetPhysicalValue(UpsUsage usage) => GetPhysicalValue(0, usage);

        private double? GetPhysicalValue(UpsUsage collection, UpsUsage usage)
        {
            if (!_usageMap.TryGetValue((collection, usage), out var mappedUsage)) { return null; }

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

        private bool? GetFlag(UpsUsage usage) => GetFlag(0, usage);

        private bool? GetFlag(UpsUsage collection, UpsUsage usage)
        {
            if (!_usageMap.TryGetValue((collection, usage), out var mappedUsage)) { return null; }

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