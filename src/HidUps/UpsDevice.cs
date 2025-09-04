using HidSharp;
using HidSharp.Reports;
using HidSharp.Reports.Encodings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HidUps;

public class Ups
{
    public HidDevice Device { get; }
    public List<HidReport> Reports { get; }

    public Ups(HidDevice device, List<HidReport> reports)
    {
        Device = device;
        Reports = reports;
    }

    public byte[]? GetFeatureReport(HidReport report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));

        if (Device.TryOpen(out var stream))
        {
            using (stream)
            {
                var buffer = new byte[Device.GetMaxFeatureReportLength()];
                if (report.ReportId != 0)
                {
                    buffer[0] = report.ReportId;
                }

                try
                {
                    stream.GetFeature(buffer);
                    return buffer;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }
        return null;
    }

    public void SetFeatureReport(HidReport report, byte[] data)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (data == null) throw new ArgumentNullException(nameof(data));

        if (Device.TryOpen(out var stream))
        {
            using (stream)
            {
                byte[] buffer;
                if (report.ReportId != 0)
                {
                    buffer = new byte[data.Length + 1];
                    buffer[0] = report.ReportId;
                    Array.Copy(data, 0, buffer, 1, data.Length);
                }
                else
                {
                    buffer = data;
                }

                stream.SetFeature(buffer);
            }
        }
    }
}

public class UpsDevice
{
    private const ushort PowerDeviceUsagePage = 0x84;

    public static IEnumerable<Ups> FindUpsDevices()
    {
        var upsDevices = new List<Ups>();
        var devices = DeviceList.Local.GetHidDevices();

        foreach (var device in devices)
        {
            try
            {
                var reportDescriptor = device.GetReportDescriptor();
                foreach (var deviceItem in reportDescriptor.DeviceItems)
                {
                    if (deviceItem.Usages.GetAllValues().Any(u => (u >> 16) == PowerDeviceUsagePage))
                    {
                        var reports = ParseReports(device);
                        upsDevices.Add(new Ups(device, reports));
                        break;
                    }
                }
            }
            catch
            {
                // Ignore devices that we can't access
            }
        }
        return upsDevices;
    }

    public static List<HidReport> ParseReports(HidDevice device)
    {
        var hidReports = new List<HidReport>();
        var reportDescriptor = device.GetReportDescriptor();

        ParseReportCollection(hidReports, reportDescriptor.FeatureReports, ReportType.Feature);
        ParseReportCollection(hidReports, reportDescriptor.InputReports, ReportType.Input);
        ParseReportCollection(hidReports, reportDescriptor.OutputReports, ReportType.Output);

        return hidReports;
    }

    private static void ParseReportCollection(List<HidReport> hidReports, IEnumerable<Report> reports, ReportType reportType)
    {
        foreach (var report in reports)
        {
            var hidReport = new HidReport
            {
                ReportId = report.ReportID,
                ReportType = reportType
            };

            foreach (var dataItem in report.DataItems)
            {
                foreach (var usageValue in dataItem.Usages.GetAllValues())
                {
                    var page = (ushort)(usageValue >> 16);
                    var id = (ushort)(usageValue & 0xFFFF);
                    var usage = new Usage(page, id);

                    var unit = new Unit((UnitSystem)dataItem.Unit.System, dataItem.Unit.CurrentExponent);

                    var reportItem = new ReportItem(usage, unit)
                    {
                        LogicalMinimum = dataItem.LogicalMinimum,
                        LogicalMaximum = dataItem.LogicalMaximum,
                    };
                    hidReport.Items.Add(reportItem);
                }
            }

            if (hidReport.Items.Any())
            {
                hidReports.Add(hidReport);
            }
        }
    }
}
