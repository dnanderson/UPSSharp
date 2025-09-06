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
    /// Represents a path through nested HID collections.
    /// </summary>
    public class CollectionPath
    {
        public UpsUsage[] Path { get; }
        
        public CollectionPath(params UpsUsage[] path)
        {
            Path = path ?? Array.Empty<UpsUsage>();
        }
        
        public override bool Equals(object obj)
        {
            if (obj is CollectionPath other)
            {
                return Path.SequenceEqual(other.Path);
            }
            return false;
        }
        
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (var usage in Path)
                {
                    hash = hash * 31 + usage.GetHashCode();
                }
                return hash;
            }
        }
        
        public override string ToString()
        {
            return string.Join(" → ", Path.Select(u => u.ToString()));
        }
    }

    /// <summary>
    /// Represents a generic, model-agnostic HID UPS device.
    /// It discovers features at runtime by parsing the HID Report Descriptor.
    /// </summary>
    public class GenericUps
    {
        private readonly IHidDevice _device;
        private readonly ReportDescriptor _reportDescriptor;
        private readonly Dictionary<(CollectionPath, UpsUsage), MappedUsage> _usageMap;

        /// <summary>
        /// A helper class to store information about a discovered HID usage.
        /// </summary>
        private class MappedUsage
        {
            public Report Report { get; }
            public DataItem DataItem { get; }
            public int DataItemBitOffset { get; }
            public int? BitIndex { get; } // Used for flags within a bitfield
            public CollectionPath CollectionPath { get; }

            public MappedUsage(Report report, DataItem dataItem, int dataItemBitOffset, 
                CollectionPath collectionPath, int? bitIndex = null)
            {
                Report = report;
                DataItem = dataItem;
                DataItemBitOffset = dataItemBitOffset;
                CollectionPath = collectionPath;
                BitIndex = bitIndex;
            }
        }

        internal GenericUps(IHidDevice device)
        {
            _device = device;
            _reportDescriptor = device.GetReportDescriptor();
            _usageMap = new Dictionary<(CollectionPath, UpsUsage), MappedUsage>();
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

        /// <summary>
        /// Gets the full collection path for a data item by walking up the parent hierarchy.
        /// </summary>
        private CollectionPath GetCollectionPath(DataItem dataItem)
        {
            var path = new List<UpsUsage>();
            var parent = dataItem.ParentItem;
            
            while (parent != null)
            {
                if (parent is DescriptorCollectionItem collectionItem)
                {
                    var usage = (UpsUsage)collectionItem.Usages.GetAllValues().FirstOrDefault();
                    if (usage != 0)
                    {
                        // Insert at beginning to build path from root to leaf
                        path.Insert(0, usage);
                    }
                }
                parent = parent.ParentItem;
            }
            
            return new CollectionPath(path.ToArray());
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
                    var collectionPath = GetCollectionPath(dataItem);
                    var usages = dataItem.Usages.GetAllValues().ToArray();

                    // Case 1: Variable bitfield (one bit per flag).
                    if (dataItem.IsVariable && dataItem.ElementBits == 1 && dataItem.ElementCount > 1)
                    {
                        int count = Math.Min(dataItem.ElementCount, usages.Length);
                        for (int i = 0; i < count; i++)
                        {
                            var usage = (UpsUsage)usages[i];
                            RegisterUsageMapping(collectionPath, usage, report, dataItem, currentBitOffset, i);
                        }
                    }
                    // Case 2: Scalar value or an Array of states.
                    else
                    {
                        // Map every possible usage associated with this item.
                        foreach (var usageValue in usages)
                        {
                            var usage = (UpsUsage)usageValue;
                            RegisterUsageMapping(collectionPath, usage, report, dataItem, currentBitOffset);
                        }
                    }
                    currentBitOffset += dataItem.TotalBits;
                }
            }
        }
        
        private void RegisterUsageMapping(CollectionPath collectionPath, UpsUsage usage, Report report, 
            DataItem dataItem, int bitOffset, int? bitIndex = null)
        {
            var mapping = new MappedUsage(report, dataItem, bitOffset, collectionPath, bitIndex);
            
            // Register with full path
            var key = (collectionPath, usage);
            if (!_usageMap.ContainsKey(key))
            {
                _usageMap[key] = mapping;
            }
            
            // Also register with partial paths for convenience
            // This allows searching by just the immediate parent collection
            if (collectionPath.Path.Length > 0)
            {
                // Register with just the last collection in the path
                var lastCollection = new CollectionPath(collectionPath.Path.Last());
                var shortKey = (lastCollection, usage);
                if (!_usageMap.ContainsKey(shortKey))
                {
                    _usageMap[shortKey] = mapping;
                }
                
                // For backwards compatibility, also register without any collection context
                var rootKey = (new CollectionPath(), usage);
                if (!_usageMap.ContainsKey(rootKey))
                {
                    _usageMap[rootKey] = mapping;
                }
            }
        }

        /// <summary>
        /// Finds a usage mapping by trying different collection path combinations.
        /// </summary>
        private MappedUsage FindUsageMapping(CollectionPath collectionPath, UpsUsage usage)
        {
            // First try exact path match
            var key = (collectionPath, usage);
            if (_usageMap.TryGetValue(key, out var mapping))
                return mapping;
            
            // If path is specified but not found, try looking for partial matches
            if (collectionPath.Path.Length > 0)
            {
                // Try matching by the leaf collection only
                var leafPath = new CollectionPath(collectionPath.Path.Last());
                key = (leafPath, usage);
                if (_usageMap.TryGetValue(key, out mapping))
                    return mapping;
            }
            
            // Finally, try without any collection context (backwards compatibility)
            key = (new CollectionPath(), usage);
            if (_usageMap.TryGetValue(key, out mapping))
                return mapping;
            
            return null;
        }

        #region Public Properties

        public string DevicePath => _device.DevicePath;
        public string Manufacturer => _device.GetManufacturer();
        public string ProductName => _device.GetProductName();

        // --- Status Properties ---
        // These use the PresentStatus bitfield which is typically in PowerSummary collection
        public bool? IsOnBattery
        {
            get
            {
                // Try PowerSummary → PresentStatus → AcPresent
                var acPresent = GetFlag(new CollectionPath(UpsUsage.PowerSummaryCollection, UpsUsage.PresentStatus), 
                    UpsUsage.AcPresent);
                if (acPresent.HasValue) return !acPresent.Value;
                
                // Fallback to direct AcPresent flag
                acPresent = GetFlag(new CollectionPath(), UpsUsage.AcPresent);
                if (acPresent.HasValue) return !acPresent.Value;
                
                return null;
            }
        }
        
        public bool? IsShutdownImminent => 
            GetFlag(new CollectionPath(UpsUsage.PowerSummaryCollection, UpsUsage.PresentStatus), 
                UpsUsage.ShutdownImminent) ?? 
            GetFlag(new CollectionPath(), UpsUsage.ShutdownImminent);
        
        public bool? IsCharging => 
            GetFlag(new CollectionPath(UpsUsage.PowerSummaryCollection, UpsUsage.PresentStatus), 
                UpsUsage.Charging) ?? 
            GetFlag(new CollectionPath(UpsUsage.BatteryCollection), UpsUsage.Charging) ??
            GetFlag(new CollectionPath(), UpsUsage.Charging);
        
        public bool? IsDischarging => 
            GetFlag(new CollectionPath(UpsUsage.PowerSummaryCollection, UpsUsage.PresentStatus), 
                UpsUsage.Discharging) ?? 
            GetFlag(new CollectionPath(), UpsUsage.Discharging);
        
        public bool? IsFullyCharged => 
            GetFlag(new CollectionPath(UpsUsage.PowerSummaryCollection, UpsUsage.PresentStatus), 
                UpsUsage.FullyCharged) ?? 
            GetFlag(new CollectionPath(), UpsUsage.FullyCharged);
        
        public bool? NeedsReplacement => 
            GetFlag(new CollectionPath(UpsUsage.PowerSummaryCollection, UpsUsage.PresentStatus), 
                UpsUsage.NeedReplacement) ?? 
            GetFlag(new CollectionPath(), UpsUsage.NeedReplacement);

        public bool? IsOverloaded => 
            GetFlag(new CollectionPath(UpsUsage.PowerConverterCollection, UpsUsage.PresentStatus), 
                UpsUsage.Overload) ?? 
            GetFlag(new CollectionPath(UpsUsage.PowerConverterCollection), UpsUsage.Overload);

        // --- Value Properties ---
        public double? RemainingCapacityPercent => 
            GetPhysicalValue(new CollectionPath(UpsUsage.PowerSummaryCollection), UpsUsage.RemainingCapacity) ??
            GetPhysicalValue(new CollectionPath(UpsUsage.BatterySystemCollection), UpsUsage.RemainingCapacity) ??
            GetPhysicalValue(new CollectionPath(), UpsUsage.RemainingCapacity);
        
        public double? RunTimeToEmptySeconds => 
            GetPhysicalValue(new CollectionPath(UpsUsage.PowerSummaryCollection), UpsUsage.RunTimeToEmpty) ??
            GetPhysicalValue(new CollectionPath(), UpsUsage.RunTimeToEmpty);
        
        public double? BatteryVoltage => 
            GetPhysicalValue(new CollectionPath(UpsUsage.BatteryCollection), UpsUsage.Voltage) ??
            GetPhysicalValue(new CollectionPath(UpsUsage.BatterySystemCollection), UpsUsage.Voltage);

        // PowerConverter has nested Input and Output collections
        public double? InputVoltage => 
            GetPhysicalValue(new CollectionPath(UpsUsage.PowerConverterCollection, UpsUsage.InputCollection), 
                UpsUsage.Voltage) ??
            GetPhysicalValue(new CollectionPath(UpsUsage.InputCollection), UpsUsage.Voltage);
        
        public double? InputFrequency => 
            GetPhysicalValue(new CollectionPath(UpsUsage.PowerConverterCollection, UpsUsage.InputCollection), 
                UpsUsage.Frequency) ??
            GetPhysicalValue(new CollectionPath(UpsUsage.InputCollection), UpsUsage.Frequency);

        public double? OutputVoltage => 
            GetPhysicalValue(new CollectionPath(UpsUsage.PowerConverterCollection, UpsUsage.OutputCollection), 
                UpsUsage.Voltage) ??
            GetPhysicalValue(new CollectionPath(UpsUsage.OutputCollection), UpsUsage.Voltage);
            
        public double? OutputFrequency => 
            GetPhysicalValue(new CollectionPath(UpsUsage.PowerConverterCollection, UpsUsage.OutputCollection), 
                UpsUsage.Frequency) ??
            GetPhysicalValue(new CollectionPath(UpsUsage.OutputCollection), UpsUsage.Frequency);
        
        public double? PercentLoad => 
            GetPhysicalValue(new CollectionPath(UpsUsage.OutletSystemCollection), UpsUsage.PercentLoad) ??
            GetPhysicalValue(new CollectionPath(), UpsUsage.PercentLoad);
        
        public double? OutputActivePowerWatts => 
            GetPhysicalValue(new CollectionPath(UpsUsage.PowerConverterCollection, UpsUsage.OutputCollection), 
                UpsUsage.ActivePower) ??
            GetPhysicalValue(new CollectionPath(UpsUsage.PowerConverterCollection), UpsUsage.ActivePower);
        
        public double? OutputApparentPowerVA => 
            GetPhysicalValue(new CollectionPath(UpsUsage.OutputCollection), UpsUsage.ApparentPower);

        #endregion

        #region Public Methods

        /// <summary>
        /// Initiates a quick self-test on the UPS.
        /// </summary>
        /// <returns>True if the command was sent successfully, otherwise false.</returns>
        public bool RunQuickTest()
        {
            // Try BatterySystem collection first (Tripplite location)
            if (SetValue(new CollectionPath(UpsUsage.BatterySystemCollection), 
                UpsUsage.Test, (int)UpsTestCommand.QuickTest))
                return true;
                
            return SetValue(new CollectionPath(), UpsUsage.Test, (int)UpsTestCommand.QuickTest);
        }

        /// <summary>
        /// Gets the current status of a self-test.
        /// </summary>
        /// <returns>The current test result, or null if the status cannot be retrieved.</returns>
        public UpsTestResult? GetTestResult()
        {
            // Try BatterySystem collection first (Tripplite location)
            var value = GetPhysicalValue(new CollectionPath(UpsUsage.BatterySystemCollection), UpsUsage.Test);
            if (value.HasValue) return (UpsTestResult?)(int)value.Value;
            
            value = GetPhysicalValue(new CollectionPath(), UpsUsage.Test);
            return value.HasValue ? (UpsTestResult?)(int)value.Value : null;
        }

        /// <summary>
        /// Dumps the usage mappings for debugging purposes.
        /// </summary>
        public void DumpMappings()
        {
            Console.WriteLine("\n=== Usage Mappings ===");
            
            // Group by collection path for easier reading
            var byPath = _usageMap.GroupBy(kvp => kvp.Key.Item1)
                .OrderBy(g => g.Key.Path.Length)
                .ThenBy(g => string.Join("/", g.Key.Path));
            
            foreach (var pathGroup in byPath)
            {
                var pathStr = pathGroup.Key.Path.Length > 0 
                    ? string.Join(" → ", pathGroup.Key.Path) 
                    : "[Root]";
                Console.WriteLine($"\n  Collection Path: {pathStr}");
                
                foreach (var kvp in pathGroup.OrderBy(k => k.Key.Item2.ToString()).Take(10))
                {
                    var usage = kvp.Key.Item2;
                    var mapped = kvp.Value;
                    Console.WriteLine($"    {usage}: Report {mapped.Report.ReportID} ({mapped.Report.ReportType}), " +
                        $"Offset: {mapped.DataItemBitOffset}, BitIndex: {mapped.BitIndex?.ToString() ?? "N/A"}");
                }
                
                if (pathGroup.Count() > 10)
                    Console.WriteLine($"    ... and {pathGroup.Count() - 10} more");
            }
        }

        /// <summary>
        /// Gets a value by specifying the full collection path.
        /// </summary>
        public double? GetValue(CollectionPath collectionPath, UpsUsage usage)
        {
            return GetPhysicalValue(collectionPath, usage);
        }

        /// <summary>
        /// Gets a flag by specifying the full collection path.
        /// </summary>
        public bool? GetFlag(CollectionPath collectionPath, UpsUsage usage)
        {
            var mapping = FindUsageMapping(collectionPath, usage);
            if (mapping == null) return null;

            var buffer = GetReport(mapping);
            if (buffer == null) return null;

            var dataItem = mapping.DataItem;

            // Case 1: Variable bitfield. Each bit is a flag.
            if (dataItem.IsVariable && mapping.BitIndex.HasValue)
            {
                if (mapping.BitIndex.Value >= dataItem.ElementCount) return null;

                // The elementIndex for ReadRaw is the bit index.
                uint rawValue = dataItem.ReadRaw(buffer, mapping.DataItemBitOffset, mapping.BitIndex.Value);
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
                        int logicalValueInReport = dataItem.ReadLogical(buffer, mapping.DataItemBitOffset, i);
                        if (logicalValueInReport == targetLogicalValue)
                        {
                            return true; // Our target usage/state is active.
                        }
                    }
                }
                return false; // The flag is not active.
            }

            // Case 3: Simple boolean in a scalar value.
            if (dataItem.IsVariable && !mapping.BitIndex.HasValue)
            {
                var logicalValue = dataItem.ReadLogical(buffer, mapping.DataItemBitOffset, 0);
                // A simple boolean is typically 1 for true, 0 for false.
                if (logicalValue == dataItem.LogicalMaximum) return true;
                if (logicalValue == dataItem.LogicalMinimum) return false;
            }

            return null; // Not a recognized flag type or unable to parse.
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

        protected bool SetValue(CollectionPath collectionPath, UpsUsage usage, int logicalValue)
        {
            var mapping = FindUsageMapping(collectionPath, usage);
            if (mapping == null) return false;

            var report = mapping.Report;
            var dataItem = mapping.DataItem;

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
                dataItem.WriteLogical(buffer, mapping.DataItemBitOffset, 0, logicalValue);

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

        private double? GetPhysicalValue(CollectionPath collectionPath, UpsUsage usage)
        {
            var mapping = FindUsageMapping(collectionPath, usage);
            if (mapping == null) return null;

            var buffer = GetReport(mapping);
            if (buffer == null) return null;

            var dataItem = mapping.DataItem;
            var logicalValue = dataItem.ReadLogical(buffer, mapping.DataItemBitOffset, 0);

            if (DataConvert.IsLogicalOutOfRange(dataItem, logicalValue))
            {
                return null;
            }

            return DataConvert.PhysicalFromLogical(dataItem, logicalValue);
        }

        #endregion
    }
}