using HidUps;
using System.Linq;
using System;

var upsDevices = UpsDevice.FindUpsDevices();

if (upsDevices.Any())
{
    Console.WriteLine($"Found {upsDevices.Count()} UPS devices.");
    foreach (var ups in upsDevices)
    {
        Console.WriteLine($"Device: {ups.Device.DevicePath}");
        Console.WriteLine("Reports:");
        foreach (var report in ups.Reports)
        {
            Console.WriteLine($"  Report ID: {report.ReportId}, Type: {report.ReportType}");
            foreach (var item in report.Items)
            {
                Console.WriteLine($"    Usage: {item.Usage.HidSharpUsage}, Min: {item.LogicalMinimum}, Max: {item.LogicalMaximum}");
            }

            if (report.ReportType == ReportType.Feature)
            {
                Console.WriteLine("    Getting feature report...");
                var featureReport = ups.GetFeatureReport(report);
                if (featureReport != null)
                {
                    Console.WriteLine($"    Feature report received: {string.Join(" ", featureReport.Select(b => b.ToString("X2")))}");
                }
                else
                {
                    Console.WriteLine("    Failed to get feature report.");
                }
            }
        }
    }
}
else
{
    Console.WriteLine("No UPS devices found.");
}
