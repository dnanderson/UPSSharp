using HidUps;
using System;
using System.Linq;
using System.Threading;
using HidSharp.Reports;

Console.WriteLine("Finding generic UPS devices...");
var upsDevices = GenericUps.FindAll().ToList();

if (upsDevices.Any())
{
    Console.WriteLine($"Found {upsDevices.Count} UPS device(s).");
    Console.WriteLine(new string('-', 30));

    foreach (var ups in upsDevices)
    {
        Console.WriteLine($"Device Path: {ups.DevicePath}");
        Console.WriteLine($"Manufacturer: {ups.Manufacturer}");
        Console.WriteLine($"Product: {ups.ProductName}");
        Console.WriteLine();

        // Let's dump the report descriptor to understand the structure
        try
        {
            var device = HidSharp.DeviceList.Local.GetHidDeviceOrNull(0x09ae);
            if (device != null)
            {
                var descriptor = device.GetReportDescriptor();
                
                Console.WriteLine("Report Analysis:");
                Console.WriteLine($"  Uses Report IDs: {descriptor.ReportsUseID}");
                Console.WriteLine();
                
                // Look for specific report IDs mentioned in Tripplite doc
                var importantReportIds = new[] { 50, 52, 53, 30, 16, 34 };
                
                foreach (var reportId in importantReportIds)
                {
                    // Check Feature Reports
                    var featureReport = descriptor.FeatureReports.FirstOrDefault(r => r.ReportID == reportId);
                    if (featureReport != null)
                    {
                        Console.WriteLine($"  Report ID {reportId} (Feature, Length: {featureReport.Length}):");
                        DumpReportItems(featureReport);
                    }
                    
                    // Check Input Reports
                    var inputReport = descriptor.InputReports.FirstOrDefault(r => r.ReportID == reportId);
                    if (inputReport != null)
                    {
                        Console.WriteLine($"  Report ID {reportId} (Input, Length: {inputReport.Length}):");
                        DumpReportItems(inputReport);
                    }
                }
                
                // Also look for reports containing RemainingCapacity usage
                var remainingCapacityUsage = 0x00850066u;
                var runTimeUsage = 0x00850068u;
                var presentStatusUsage = 0x00840002u;
                
                Console.WriteLine("\nSearching for specific usages:");
                
                foreach (var report in descriptor.FeatureReports.Concat(descriptor.InputReports))
                {
                    foreach (var item in report.DataItems)
                    {
                        var usages = item.Usages.GetAllValues().ToList();
                        
                        if (usages.Contains(remainingCapacityUsage))
                        {
                            Console.WriteLine($"  Found RemainingCapacity in Report {report.ReportID} ({report.ReportType})");
                            Console.WriteLine($"    Logical Min/Max: {item.LogicalMinimum}/{item.LogicalMaximum}");
                            Console.WriteLine($"    Physical Min/Max: {item.PhysicalMinimum}/{item.PhysicalMaximum}");
                            Console.WriteLine($"    Unit: {item.Unit.System}");
                        }
                        
                        if (usages.Contains(runTimeUsage))
                        {
                            Console.WriteLine($"  Found RunTimeToEmpty in Report {report.ReportID} ({report.ReportType})");
                            Console.WriteLine($"    Logical Min/Max: {item.LogicalMinimum}/{item.LogicalMaximum}");
                            Console.WriteLine($"    Physical Min/Max: {item.PhysicalMinimum}/{item.PhysicalMaximum}");
                            Console.WriteLine($"    Unit: {item.Unit.System}");
                        }
                        
                        if (usages.Contains(presentStatusUsage))
                        {
                            Console.WriteLine($"  Found PresentStatus in Report {report.ReportID} ({report.ReportType})");
                            Console.WriteLine($"    Element Count: {item.ElementCount}, Element Bits: {item.ElementBits}");
                            Console.WriteLine($"    Is Variable: {item.IsVariable}, Is Array: {item.IsArray}");
                            
                            // Show what specific flags are in this bitfield
                            var flagUsages = item.Usages.GetAllValues().ToList();
                            Console.WriteLine($"    Bitfield contains {flagUsages.Count} flags:");
                            for (int i = 0; i < flagUsages.Count && i < 8; i++)
                            {
                                var usage = (HidUps.UpsUsage)flagUsages[i];
                                Console.WriteLine($"      Bit {i}: {usage}");
                            }
                        }
                    }
                }
                
                // Try reading raw report data
                Console.WriteLine("\nAttempting to read raw report data:");
                if (device.TryOpen(out var stream))
                {
                    using (stream)
                    {
                        stream.ReadTimeout = 2000;
                        
                        // Try to read Report ID 52 (RemainingCapacity)
                        try
                        {
                            var buffer52 = new byte[10]; // Adjust size as needed
                            buffer52[0] = 52;
                            stream.GetFeature(buffer52);
                            Console.WriteLine($"  Report 52 raw data: {BitConverter.ToString(buffer52)}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Could not read Report 52: {ex.Message}");
                        }
                        
                        // Try to read Report ID 53 (RunTimeToEmpty)
                        try
                        {
                            var buffer53 = new byte[10];
                            buffer53[0] = 53;
                            stream.GetFeature(buffer53);
                            Console.WriteLine($"  Report 53 raw data: {BitConverter.ToString(buffer53)}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Could not read Report 53: {ex.Message}");
                        }
                        
                        // Try to read Report ID 50 (PresentStatus)
                        try
                        {
                            var buffer50 = new byte[10];
                            buffer50[0] = 50;
                            stream.GetFeature(buffer50);
                            Console.WriteLine($"  Report 50 raw data: {BitConverter.ToString(buffer50)}");
                            
                            // Decode the status bits
                            if (buffer50.Length > 1)
                            {
                                byte statusByte = buffer50[1];
                                Console.WriteLine($"    Bit 0 (ShutdownImminent): {(statusByte & 0x01) != 0}");
                                Console.WriteLine($"    Bit 1 (ACPresent): {(statusByte & 0x02) != 0}");
                                Console.WriteLine($"    Bit 2 (Charging): {(statusByte & 0x04) != 0}");
                                Console.WriteLine($"    Bit 3 (Discharging): {(statusByte & 0x08) != 0}");
                                Console.WriteLine($"    Bit 4 (NeedReplacement): {(statusByte & 0x10) != 0}");
                                Console.WriteLine($"    Bit 5 (BelowRemainingCapacityLimit): {(statusByte & 0x20) != 0}");
                                Console.WriteLine($"    Bit 6 (FullyCharged): {(statusByte & 0x40) != 0}");
                                Console.WriteLine($"    Bit 7 (FullyDischarged): {(statusByte & 0x80) != 0}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Could not read Report 50: {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error analyzing device: {ex.Message}");
        }

        // Add mapping dump for the GenericUps
        ups.DumpMappings();
        // --- Value Properties ---
        Console.WriteLine($"  Input Voltage: {ups.InputVoltage?.ToString("F1") ?? "N/A"} V");
        Console.WriteLine($"  Input Frequency: {ups.InputFrequency?.ToString("F1") ?? "N/A"} Hz");
        Console.WriteLine($"  Output Voltage: {ups.OutputVoltage?.ToString("F1") ?? "N/A"} V");
        Console.WriteLine($"  Percent Load: {ups.PercentLoad?.ToString("F0") ?? "N/A"} %");
        Console.WriteLine($"  Remaining Capacity: {ups.RemainingCapacityPercent?.ToString("F0") ?? "N/A"} %");
        Console.WriteLine($"  Runtime to Empty: {ups.RunTimeToEmptySeconds?.ToString("F0") ?? "N/A"} sec");
        Console.WriteLine($"  Battery Voltage: {ups.BatteryVoltage?.ToString("F1") ?? "N/A"} V");
        
        Console.WriteLine("\nFlags:");
        Console.WriteLine($"  On Battery: {ups.IsOnBattery?.ToString() ?? "N/A"}");
        Console.WriteLine($"  Charging: {ups.IsCharging?.ToString() ?? "N/A"}");
        Console.WriteLine($"  Discharging: {ups.IsDischarging?.ToString() ?? "N/A"}");
        Console.WriteLine($"  Fully Charged: {ups.IsFullyCharged?.ToString() ?? "N/A"}");
        Console.WriteLine($"  Needs Replacement: {ups.NeedsReplacement?.ToString() ?? "N/A"}");
        Console.WriteLine($"  Overloaded: {ups.IsOverloaded?.ToString() ?? "N/A"}");
        Console.WriteLine($"  Shutdown Imminent: {ups.IsShutdownImminent?.ToString() ?? "N/A"}");

        Console.WriteLine(new string('-', 30));
    }
}
else
{
    Console.WriteLine("No generic UPS devices found.");
}

void DumpReportItems(Report report)
{
    foreach (var item in report.DataItems)
    {
        var usages = item.Usages.GetAllValues().Select(u => $"0x{u:X8}").ToList();
        Console.WriteLine($"    DataItem: {string.Join(", ", usages)}");
        Console.WriteLine($"      Bits: {item.ElementBits}, Count: {item.ElementCount}, Total: {item.TotalBits}");
    }
}