using HidUps;
using System;
using System.Linq;
using System.Threading;

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
        Console.WriteLine($"Serial: {ups.SerialNumber}");
        Console.WriteLine();
        Console.WriteLine("Status:");

        // --- Value Properties ---
        Console.WriteLine($"  Input Voltage: {ups.InputVoltage?.ToString("F1") ?? "N/A"} V");
        Console.WriteLine($"  Percent Load: {ups.PercentLoad?.ToString("F0") ?? "N/A"} %");
        Console.WriteLine($"  Remaining Capacity: {ups.RemainingCapacityPercent?.ToString("F0") ?? "N/A"} %");
        Console.WriteLine($"  Runtime to Empty: {ups.RunTimeToEmptySeconds?.ToString("F0") ?? "N/A"} sec");
        Console.WriteLine($"  Output Power (Active): {ups.OutputActivePowerWatts?.ToString("F0") ?? "N/A"} W");
        Console.WriteLine($"  Output Power (Apparent): {ups.OutputApparentPowerVA?.ToString("F0") ?? "N/A"} VA");
        
        Console.WriteLine();
        Console.WriteLine("Flags:");
        
        // --- Status Flags ---
        Console.WriteLine($"  On Battery: {ups.IsOnBattery?.ToString() ?? "N/A"}");
        Console.WriteLine($"  Charging: {ups.IsCharging?.ToString() ?? "N/A"}");
        Console.WriteLine($"  Discharging: {ups.IsDischarging?.ToString() ?? "N/A"}");
        Console.WriteLine($"  Fully Charged: {ups.IsFullyCharged?.ToString() ?? "N/A"}");
        Console.WriteLine($"  Needs Replacement: {ups.NeedsReplacement?.ToString() ?? "N/A"}");
        Console.WriteLine($"  Overloaded: {ups.IsOverloaded?.ToString() ?? "N/A"}");
        Console.WriteLine($"  Shutdown Imminent: {ups.IsShutdownImminent?.ToString() ?? "N/A"}");
        Console.WriteLine();

        // --- Test Functionality ---
        Console.WriteLine("Running self-test...");
        if (ups.RunQuickTest())
        {
            Console.WriteLine("  Test command sent successfully.");
            UpsTestResult? testResult;
            int attempts = 0;
            do
            {
                Thread.Sleep(3000); // Wait 2 seconds between checks
                testResult = ups.GetTestResult();
                Console.WriteLine($"  Current test status: {testResult?.ToString() ?? "Unknown"}");
                attempts++;
            } while (attempts < 10 && testResult == UpsTestResult.InProgress);

            Console.WriteLine($"  Final test result: {testResult?.ToString() ?? "Unknown"}");
        }
        else
        {
            Console.WriteLine("  Failed to send test command. This feature may not be supported.");
        }


        Console.WriteLine(new string('-', 30));
    }
}
else
{
    Console.WriteLine("No generic UPS devices found.");
}

