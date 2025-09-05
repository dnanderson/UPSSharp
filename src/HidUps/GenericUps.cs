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
    /// It discovers features at runtime by parsing the HID Report Descriptor,
    /// including support for nested collections.
    /// </summary>
    public class GenericUps
    {
        /// <summary>
        /// Represents the full hierarchical path to a specific usage.
        /// Used as a key for mapping discovered usages.
        /// </summary>
        private readonly struct UsagePath : IEquatable<UsagePath>
        {
            public readonly IReadOnlyList<UpsUsage> Collections { get; }
            public readonly UpsUsage Usage { get; }

            public UsagePath(IReadOnlyList<UpsUsage> collections, UpsUsage usage)
            {
                Collections = collections ?? new List<UpsUsage>();
                Usage = usage;
            }

            public bool Equals(UsagePath other)
            {
                if (Usage != other.Usage) return false;
                if (Collections.Count != other.Collections.Count) return false;
                return Collections.SequenceEqual(other.Collections);
            }

            public override bool Equals(object obj)
            {
                return obj is UsagePath other && Equals(other);
            }

            public override int GetHashCode()
            {
                int hashCode = Usage.GetHashCode();
                foreach (var collection in Collections)
                {
                    // Simple hash combining algorithm
                    hashCode = (hashCode * 397) ^ collection.GetHashCode();
                }
                return hashCode;
            }

            public override string ToString()
            {
                if (Collections.Count == 0) return Usage.ToString();
                return $"{string.Join("/", Collections)}/{Usage}";
            }
        }

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

        private readonly IHidDevice _device;
        private readonly ReportDescriptor _reportDescriptor;
        private readonly Dictionary<UsagePath, MappedUsage> _usageMap;

        internal GenericUps(IHidDevice device)
        {
            _device = device;
            _reportDescriptor = device.GetReportDescriptor();
            _usageMap = new Dictionary<UsagePath, MappedUsage>();
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
        /// Walks the parent hierarchy of a DataItem to determine its full collection path.
        /// </summary>
        private List<UpsUsage> GetCollectionPath(DataItem dataItem)
        {
            var path = new List<UpsUsage>();
            var parent = dataItem.ParentItem;
            while (parent != null)
            {
                if (parent is DescriptorCollectionItem collectionItem)
                {
                    // Get the usage of this collection
                    var usage = (UpsUsage)collectionItem.Usages.GetAllValues().FirstOrDefault();
                    if (usage != 0)
                    {
                        path.Add(usage);
                    }
                }
                parent = parent.ParentItem;
            }
            path.Reverse(); // Order from outermost to innermost
            return path;
        }

        /// <summary>
        /// Parses the device's report descriptor to map all usages with their full collection paths.
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
        
        /// <summary>
        /// Registers a discovered usage with its full collection path in the map.
        /// </summary>
        private void RegisterUsageMapping(IReadOnlyList<UpsUsage> collectionPath, UpsUsage usage, Report report,
            DataItem dataItem, int bitOffset, int? bitIndex = null)
        {
            var key = new UsagePath(collectionPath, usage);
            // Let the last parsed item win in case of duplicates, similar to HidSharp's behavior.
            _usageMap[key] = new MappedUsage(report, dataItem, bitOffset, bitIndex);
        }

        #region Public Properties

        public string DevicePath => _device.DevicePath;
        public string Manufacturer => _device.GetManufacturer();
        public string ProductName => _device.GetProductName();
        // This one doesn't seem to want to work?
        // public string SerialNumber => _device.GetSerialNumber();

        // --- Status Properties ---
        public bool? IsOnBattery
        {
            get
            {
                var acPresent = GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.AcPresent) ?? GetFlag(UpsUsage.AcPresent);
                if (acPresent.HasValue) return !acPresent.Value;
                return null;
            }
        }
        
        public bool? IsShutdownImminent => GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.ShutdownImminent) ?? GetFlag(UpsUsage.ShutdownImminent);
        public bool? IsCharging => GetFlag(UpsUsage.BatterySystemCollection, UpsUsage.Charging) ?? GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.Charging) ?? GetFlag(UpsUsage.Charging);
        public bool? IsDischarging => GetFlag(UpsUsage.BatterySystemCollection, UpsUsage.Discharging) ?? GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.Discharging) ?? GetFlag(UpsUsage.Discharging);
        public bool? IsFullyCharged => GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.FullyCharged) ?? GetFlag(UpsUsage.FullyCharged);
        public bool? NeedsReplacement => GetFlag(UpsUsage.BatterySystemCollection, UpsUsage.NeedReplacement) ?? GetFlag(UpsUsage.PowerSummaryCollection, UpsUsage.NeedReplacement) ?? GetFlag(UpsUsage.NeedReplacement);
        public bool? IsOverloaded => GetFlag(UpsUsage.PowerConverterCollection, UpsUsage.Overload) ?? GetFlag(UpsUsage.Overload);

        // --- Value Properties ---
        public double? RemainingCapacityPercent => GetPhysicalValue(UpsUsage.PowerSummaryCollection, UpsUsage.RemainingCapacity) ?? GetPhysicalValue(UpsUsage.RemainingCapacity);
        public double? RunTimeToEmptySeconds => GetPhysicalValue(UpsUsage.PowerSummaryCollection, UpsUsage.RunTimeToEmpty) ?? GetPhysicalValue(UpsUsage.RunTimeToEmpty);
        public double? BatteryVoltage => GetPhysicalValue(UpsUsage.BatterySystemCollection, UpsUsage.BatteryCollection, UpsUsage.Voltage) ?? GetPhysicalValue(UpsUsage.BatterySystemCollection, UpsUsage.Voltage) ?? GetPhysicalValue(UpsUsage.BatteryCollection, UpsUsage.Voltage);
        public double? InputVoltage => GetPhysicalValue(UpsUsage.PowerConverterCollection, UpsUsage.InputCollection, UpsUsage.Voltage) ?? GetPhysicalValue(UpsUsage.InputCollection, UpsUsage.Voltage) ?? GetPhysicalValue(UpsUsage.Voltage);
        public double? InputFrequency => GetPhysicalValue(UpsUsage.PowerConverterCollection, UpsUsage.InputCollection, UpsUsage.Frequency) ?? GetPhysicalValue(UpsUsage.InputCollection, UpsUsage.Frequency) ?? GetPhysicalValue(UpsUsage.Frequency);
        public double? OutputVoltage => GetPhysicalValue(UpsUsage.PowerConverterCollection, UpsUsage.OutputCollection, UpsUsage.Voltage) ?? GetPhysicalValue(UpsUsage.OutputCollection, UpsUsage.Voltage);
        public double? OutputFrequency => GetPhysicalValue(UpsUsage.PowerConverterCollection, UpsUsage.OutputCollection, UpsUsage.Frequency) ?? GetPhysicalValue(UpsUsage.OutputCollection, UpsUsage.Frequency);
        public double? PercentLoad => GetPhysicalValue(UpsUsage.OutletSystemCollection, UpsUsage.PercentLoad) ?? GetPhysicalValue(UpsUsage.PercentLoad);
        public double? OutputActivePowerWatts => GetPhysicalValue(UpsUsage.PowerConverterCollection, UpsUsage.ActivePower) ?? GetPhysicalValue(UpsUsage.ActivePower);
        public double? OutputApparentPowerVA => GetPhysicalValue(UpsUsage.OutputCollection, UpsUsage.ApparentPower) ?? GetPhysicalValue(UpsUsage.ApparentPower);

        #endregion

        #region Public Methods
        
        /// <summary>
        /// Dumps all discovered usage paths to the console for debugging.
        /// </summary>
        public void DumpMappings()
        {
            Console.WriteLine("\n=== Discovered HID Usage Mappings ===");
            if (_usageMap.Any())
            {
                foreach (var mapping in _usageMap.OrderBy(kvp => kvp.Key.ToString()))
                {
                    var key = mapping.Key;
                    var mapped = mapping.Value;
                    Console.WriteLine($"  Path: {key}");
                    Console.WriteLine($"    -> Report ID: {mapped.Report.ReportID}, Type: {mapped.Report.ReportType}, Bit Offset: {mapped.DataItemBitOffset}, Bit Index: {mapped.BitIndex?.ToString() ?? "N/A"}");
                }
            }
            else
            {
                Console.WriteLine("  No mappings found.");
            }
            Console.WriteLine("=====================================\n");
        }
        
        /// <summary>
        /// Initiates a quick self-test on the UPS.
        /// </summary>
        /// <returns>True if the command was sent successfully, otherwise false.</returns>
        public bool RunQuickTest()
        {
            return SetValue((int)UpsTestCommand.QuickTest, UpsUsage.BatterySystemCollection, UpsUsage.Test) ||
                   SetValue((int)UpsTestCommand.QuickTest, UpsUsage.Test);
        }

        /// <summary>
        /// Gets the current status of a self-test.
        /// </summary>
        /// <returns>The current test result, or null if the status cannot be retrieved.</returns>
        public UpsTestResult? GetTestResult()
        {
            var value = GetPhysicalValue(UpsUsage.BatterySystemCollection, UpsUsage.Test) ?? GetPhysicalValue(UpsUsage.Test);
            return value.HasValue ? (UpsTestResult?)(int)value.Value : null;
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Finds a mapped usage by its path. Supports exact and partial/unambiguous path matching.
        /// </summary>
        /// <param name="usagePath">The path to search for, ending with the target usage.</param>
        /// <returns>The MappedUsage if found, otherwise null.</returns>
        private MappedUsage FindUsage(params UpsUsage[] usagePath)
        {
            if (usagePath == null || usagePath.Length == 0) return null;

            var targetUsage = usagePath.Last();
            var targetCollections = new ArraySegment<UpsUsage>(usagePath, 0, usagePath.Length - 1);

            // First, try for an exact match on the full path provided.
            var exactKey = new UsagePath(targetCollections.ToList(), targetUsage);
            if (_usageMap.TryGetValue(exactKey, out var mappedUsage))
            {
                return mappedUsage;
            }
            
            // If no exact match, find all candidates where the path *ends with* the provided path.
            var candidates = _usageMap.Where(kvp =>
                kvp.Key.Usage == targetUsage &&
                kvp.Key.Collections.Count >= targetCollections.Count &&
                kvp.Key.Collections.Skip(kvp.Key.Collections.Count - targetCollections.Count).SequenceEqual(targetCollections)
            ).ToList();

            // If there's only one such candidate, it's an unambiguous match.
            if (candidates.Count == 1)
            {
                return candidates.Single().Value;
            }

            // More than one match means the provided path is ambiguous. No match means it wasn't found.
            return null;
        }

        private byte[] GetReport(MappedUsage mappedUsage)
        {
            if (mappedUsage.Report.ReportType != ReportType.Feature &&
                mappedUsage.Report.ReportType != ReportType.Input)
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
                    if (mappedUsage.Report.ReportType == ReportType.Feature)
                    {
                        stream.GetFeature(buffer);
                    }
                    else // Input Report
                    {
                        // Note: A simple Read() might not be reliable for all devices/reports.
                        // For robust input report handling, a dedicated reading thread or
                        // the HidDeviceInputReceiver is recommended. This is a simplification.
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

        protected bool SetValue(int logicalValue, params UpsUsage[] usagePath)
        {
            var mappedUsage = FindUsage(usagePath);
            if (mappedUsage == null) { return false; }

            var report = mappedUsage.Report;
            var dataItem = mappedUsage.DataItem;

            if (report.ReportType != ReportType.Feature && report.ReportType != ReportType.Output)
            {
                return false;
            }

            if (!_device.TryOpen(out IHidStream stream)) { return false; }

            using (stream)
            {
                stream.WriteTimeout = 2000;
                // Get the current state of the report first to avoid altering other values.
                var buffer = new byte[report.Length];
                if (report.ReportID != 0) buffer[0] = report.ReportID;

                if (report.ReportType == ReportType.Feature)
                {
                    try { stream.GetFeature(buffer); } catch { /* Ignore failure, proceed with empty buffer */ }
                }

                dataItem.WriteLogical(buffer, mappedUsage.DataItemBitOffset, 0, logicalValue);

                try
                {
                    if (report.ReportType == ReportType.Feature)
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

        protected double? GetPhysicalValue(params UpsUsage[] usagePath)
        {
            var mappedUsage = FindUsage(usagePath);
            if (mappedUsage == null) { return null; }

            var buffer = GetReport(mappedUsage);
            if (buffer == null) { return null; }

            var dataItem = mappedUsage.DataItem;
            var logicalValue = dataItem.ReadLogical(buffer, mappedUsage.DataItemBitOffset, 0);

            if (DataConvert.IsLogicalOutOfRange(dataitem, logicalValue))
            {
                return null;
            }

            return DataConvert.PhysicalFromLogical(dataItem, logicalValue);
        }

        protected bool? GetFlag(params UpsUsage[] usagePath)
        {
            var mappedUsage = FindUsage(usagePath);
            if (mappedUsage == null) { return null; }

            var buffer = GetReport(mappedUsage);
            if (buffer == null) { return null; }

            var dataItem = mappedUsage.DataItem;

            if (dataItem.IsVariable && mappedUsage.BitIndex.HasValue)
            {
                if (mappedUsage.BitIndex.Value >= dataItem.ElementCount) return null;
                uint rawValue = dataItem.ReadRaw(buffer, mappedUsage.DataItemBitOffset, mappedUsage.BitIndex.Value);
                return rawValue == 1;
            }

            if (dataItem.IsArray)
            {
                var targetUsage = usagePath.Last();
                if (dataItem.Usages.TryGetIndexFromValue((uint)targetUsage, out int targetUsageIndex))
                {
                    int targetLogicalValue = targetUsageIndex + dataItem.LogicalMinimum;
                    for (int i = 0; i < dataItem.ElementCount; i++)
                    {
                        if (dataItem.ReadLogical(buffer, mappedUsage.DataItemBitOffset, i) == targetLogicalValue)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

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
