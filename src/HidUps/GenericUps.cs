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
    /// Represents a path through the collection hierarchy to a specific usage.
    /// </summary>
    public class CollectionPath
    {
        public List<UpsUsage> Path { get; }
        
        public CollectionPath(params UpsUsage[] path)
        {
            Path = new List<UpsUsage>(path);
        }
        
        public CollectionPath(List<UpsUsage> path)
        {
            Path = new List<UpsUsage>(path);
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
            int hash = 17;
            foreach (var usage in Path)
            {
                hash = hash * 31 + usage.GetHashCode();
            }
            return hash;
        }
        
        public override string ToString()
        {
            return string.Join(" -> ", Path);
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
        private readonly Dictionary<CollectionPath, MappedUsage> _usageMap;

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
            _usageMap = new Dictionary<CollectionPath, MappedUsage>();
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
                                ((usage >> 16) == powerDeviceUsagePage) && 
                                (usage == upsUsage || usage == powerSupplyUsage)
                            )
                        );
                    }
                    catch
                    {
                        return false;
                    }
                });

            foreach (var device in devices)
            {
                yield return new GenericUps(new HidDeviceWrapper(device));
            }
        }

        /// <summary>
        /// Gets the full collection path for a data item by walking up the parent chain.
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
            
            return new CollectionPath(path);
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
                int currentBitOffset = _reportDescriptor.ReportsUseID ? 8 : 0;
                
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
        
        private void RegisterUsageMapping(CollectionPath collectionPath, UpsUsage usage, 
            Report report, DataItem dataItem, int bitOffset, int? bitIndex = null)
        {
            // Create the full path including the usage itself
            var fullPath = new CollectionPath(collectionPath.Path.Concat(new[] { usage }).ToList());
            
            if (!_usageMap.ContainsKey(fullPath))
            {
                _usageMap[fullPath] = new MappedUsage(report, dataItem, bitOffset, collectionPath, bitIndex);
            }
        }

        /// <summary>
        /// Finds a usage given a collection path specification.
        /// </summary>
        private MappedUsage FindUsage(params UpsUsage[] pathSpec)
        {
            var searchPath = new CollectionPath(pathSpec);
            
            // Try exact match first
            if (_usageMap.TryGetValue(searchPath, out var exact))
            {
                return exact;
            }
            
            // If path spec has at least 2 elements, try finding it as a suffix match
            // This handles cases where we specify a partial path
            if (pathSpec.Length >= 2)
            {
                foreach (var kvp in _usageMap)
                {
                    var fullPath = kvp.Key.Path;
                    if (fullPath.Count >= pathSpec.Length)
                    {
                        // Check if the path spec matches the end of the full path
                        bool matches = true;
                        for (int i = 0; i < pathSpec.Length; i++)
                        {
                            if (fullPath[fullPath.Count - pathSpec.Length + i] != pathSpec[i])
                            {
                                matches = false;
                                break;
                            }
                        }
                        if (matches)
                        {
                            return kvp.Value;
                        }
                    }
                }
            }
            
            return null;
        }

        #region Public Properties

        public string DevicePath => _device.DevicePath;
        public string Manufacturer => _device.GetManufacturer();
        public string ProductName => _device.GetProductName();

        // --- Status Properties ---
        // These use the path-based lookup to find the correct usage in the correct collection
        
        public bool? IsOnBattery
        {
            get
            {
                // AC Present can be in PowerSummary or standalone
                var acPresent = GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.PresentStatus, UpsUsage.AcPresent) 
                             ?? GetFlag(UpsUsage.AcPresent);
                if (acPresent.HasValue) return !acPresent.Value;
                return null;
            }
        }
        
        public bool? IsShutdownImminent => 
            GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.PresentStatus, UpsUsage.ShutdownImminent)
            ?? GetFlag(UpsUsage.ShutdownImminent);
        
        public bool? IsCharging => 
            GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.PresentStatus, UpsUsage.Charging)
            ?? GetFlag(UpsUsage.BatterySystemCollection, UpsUsage.Charging)
            ?? GetFlag(UpsUsage.Charging);
        
        public bool? IsDischarging => 
            GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.PresentStatus, UpsUsage.Discharging)
            ?? GetFlag(UpsUsage.BatterySystemCollection, UpsUsage.Discharging)
            ?? GetFlag(UpsUsage.Discharging);
        
        public bool? IsFullyCharged => 
            GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.PresentStatus, UpsUsage.FullyCharged)
            ?? GetFlag(UpsUsage.FullyCharged);
        
        public bool? NeedsReplacement => 
            GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.PresentStatus, UpsUsage.NeedReplacement)
            ?? GetFlag(UpsUsage.BatterySystemCollection, UpsUsage.NeedReplacement)
            ?? GetFlag(UpsUsage.NeedReplacement);

        public bool? IsOverloaded => 
            GetFlag(UpsUsage.PowerConverterCollection, UpsUsage.PresentStatus, UpsUsage.Overload)
            ?? GetFlag(UpsUsage.PowerConverterCollection, UpsUsage.Overload)
            ?? GetFlag(UpsUsage.Overload);

        // --- Value Properties ---
        
        public double? RemainingCapacityPercent => 
            GetPhysicalValue(UpsUsage.PowerSummaryCollection, UpsUsage.RemainingCapacity)
            ?? GetPhysicalValue(UpsUsage.BatterySystemCollection, UpsUsage.RemainingCapacity)
            ?? GetPhysicalValue(UpsUsage.RemainingCapacity);
        
        public double? RunTimeToEmptySeconds => 
            GetPhysicalValue(UpsUsage.PowerSummaryCollection, UpsUsage.RunTimeToEmpty)
            ?? GetPhysicalValue(UpsUsage.BatterySystemCollection, UpsUsage.RunTimeToEmpty)
            ?? GetPhysicalValue(UpsUsage.RunTimeToEmpty);
        
        public double? BatteryVoltage => 
            GetPhysicalValue(UpsUsage.BatterySystemCollection, UpsUsage.BatteryCollection, UpsUsage.Voltage)
            ?? GetPhysicalValue(UpsUsage.BatteryCollection, UpsUsage.Voltage);

        public double? InputVoltage => 
            GetPhysicalValue(UpsUsage.PowerConverterCollection, UpsUsage.InputCollection, UpsUsage.Voltage)
            ?? GetPhysicalValue(UpsUsage.FlowCollection, UpsUsage.InputCollection, UpsUsage.Voltage)
            ?? GetPhysicalValue(UpsUsage.InputCollection, UpsUsage.Voltage);
        
        public double? InputFrequency => 
            GetPhysicalValue(UpsUsage.PowerConverterCollection, UpsUsage.InputCollection, UpsUsage.Frequency)
            ?? GetPhysicalValue(UpsUsage.FlowCollection, UpsUsage.InputCollection, UpsUsage.Frequency)
            ?? GetPhysicalValue(UpsUsage.InputCollection, UpsUsage.Frequency);

        public double? OutputVoltage => 
            GetPhysicalValue(UpsUsage.PowerConverterCollection, UpsUsage.OutputCollection, UpsUsage.Voltage)
            ?? GetPhysicalValue(UpsUsage.FlowCollection, UpsUsage.OutputCollection, UpsUsage.Voltage)
            ?? GetPhysicalValue(UpsUsage.OutputCollection, UpsUsage.Voltage);
            
        public double? OutputFrequency => 
            GetPhysicalValue(UpsUsage.PowerConverterCollection, UpsUsage.OutputCollection, UpsUsage.Frequency)
            ?? GetPhysicalValue(UpsUsage.OutputCollection, UpsUsage.Frequency);
        
        public double? PercentLoad => 
            GetPhysicalValue(UpsUsage.OutletSystemCollection, UpsUsage.PercentLoad)
            ?? GetPhysicalValue(UpsUsage.PercentLoad);
        
        public double? OutputActivePowerWatts => 
            GetPhysicalValue(UpsUsage.PowerConverterCollection, UpsUsage.OutputCollection, UpsUsage.ActivePower)
            ?? GetPhysicalValue(UpsUsage.OutputCollection, UpsUsage.ActivePower)
            ?? GetPhysicalValue(UpsUsage.ActivePower);

        // Debugging method
        public void DumpMappings()
        {
            Console.WriteLine("\n=== Usage Mappings (Hierarchical) ===");
            
            var groupedByReport = _usageMap.GroupBy(kvp => kvp.Value.Report.ReportID).OrderBy(g => g.Key);
            
            foreach (var reportGroup in groupedByReport)
            {
                Console.WriteLine($"\nReport ID {reportGroup.Key}:");
                foreach (var kvp in reportGroup.OrderBy(k => k.Key.ToString()))
                {
                    var mapped = kvp.Value;
                    Console.WriteLine($"  {kvp.Key}");
                    Console.WriteLine($"    Type: {mapped.Report.ReportType}, BitOffset: {mapped.DataItemBitOffset}, BitIndex: {mapped.BitIndex}");
                }
            }
        }
        
        #endregion

        #region Public Methods

        public bool RunQuickTest()
        {
            return SetValue(new[] { UpsUsage.BatterySystemCollection, UpsUsage.Test }, (int)UpsTestCommand.QuickTest)
                || SetValue(new[] { UpsUsage.Test }, (int)UpsTestCommand.QuickTest);
        }

        public UpsTestResult? GetTestResult()
        {
            var value = GetPhysicalValue(UpsUsage.BatterySystemCollection, UpsUsage.Test)
                     ?? GetPhysicalValue(UpsUsage.Test);
            return value.HasValue ? (UpsTestResult?)(int)value.Value : null;
        }

        #endregion

        #region Private Helper Methods

        private byte[] GetReport(MappedUsage mappedUsage)
        {
            if (mappedUsage.Report.ReportType != HidSharp.Reports.ReportType.Feature &&
                mappedUsage.Report.ReportType != HidSharp.Reports.ReportType.Input)
            {
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

        protected bool SetValue(UpsUsage[] pathSpec, int logicalValue)
        {
            var mappedUsage = FindUsage(pathSpec);
            if (mappedUsage == null) return false;

            var report = mappedUsage.Report;
            var dataItem = mappedUsage.DataItem;

            if (report.ReportType != HidSharp.Reports.ReportType.Feature &&
                report.ReportType != HidSharp.Reports.ReportType.Output)
            {
                return false;
            }

            if (!_device.TryOpen(out IHidStream stream)) { return false; }

            using (stream)
            {
                stream.WriteTimeout = 2000;
                var buffer = new byte[report.Length];
                buffer[0] = report.ReportID;

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

        private double? GetPhysicalValue(params UpsUsage[] pathSpec)
        {
            var mappedUsage = FindUsage(pathSpec);
            if (mappedUsage == null) return null;

            var buffer = GetReport(mappedUsage);
            if (buffer == null) return null;

            var dataItem = mappedUsage.DataItem;
            var logicalValue = dataItem.ReadLogical(buffer, mappedUsage.DataItemBitOffset, 0);

            if (DataConvert.IsLogicalOutOfRange(dataItem, logicalValue))
            {
                return null;
            }

            return DataConvert.PhysicalFromLogical(dataItem, logicalValue);
        }

        private bool? GetFlag(params UpsUsage[] pathSpec)
        {
            var mappedUsage = FindUsage(pathSpec);
            if (mappedUsage == null) return null;

            var buffer = GetReport(mappedUsage);
            if (buffer == null) return null;

            var dataItem = mappedUsage.DataItem;
            var targetUsage = pathSpec[pathSpec.Length - 1];

            // Case 1: Variable bitfield. Each bit is a flag.
            if (dataItem.IsVariable && mappedUsage.BitIndex.HasValue)
            {
                if (mappedUsage.BitIndex.Value >= dataItem.ElementCount) return null;

                uint rawValue = dataItem.ReadRaw(buffer, mappedUsage.DataItemBitOffset, mappedUsage.BitIndex.Value);
                return rawValue == 1;
            }

            // Case 2: Array of selectors.
            if (dataItem.IsArray)
            {
                if (dataItem.Usages.TryGetIndexFromValue((uint)targetUsage, out int targetUsageIndex))
                {
                    int targetLogicalValue = targetUsageIndex + dataItem.LogicalMinimum;

                    for (int i = 0; i < dataItem.ElementCount; i++)
                    {
                        int logicalValueInReport = dataItem.ReadLogical(buffer, mappedUsage.DataItemBitOffset, i);
                        if (logicalValueInReport == targetLogicalValue)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            // Case 3: Simple boolean in a scalar value.
            if (dataItem.IsVariable && !mappedUsage.BitIndex.HasValue)
            {
                var logicalValue = dataItem.ReadLogical(buffer, mappedUsage.DataItemBitOffset, 0);
                if (logicalValue == dataItem.LogicalMaximum) return true;
                if (logicalValue == dataItem.LogicalMinimum) return false;
            }

            return null;
        }

        #endregion
    }
}