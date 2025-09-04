using HidSharp.Reports;

namespace HidUps;

public class HidReport
{
    public byte ReportId { get; set; }
    public ReportType ReportType { get; set; }
    public List<ReportItem> Items { get; set; } = new List<ReportItem>();
}

public enum ReportType
{
    Input,
    Output,
    Feature
}

public class ReportItem
{
    public Usage Usage { get; set; }
    public int Value { get; set; }
    public Unit Unit { get; set; }
    public int LogicalMinimum { get; set; }
    public int LogicalMaximum { get; set; }

    public ReportItem(Usage usage, Unit unit)
    {
        Usage = usage;
        Unit = unit;
    }
}

public class Usage
{
    public ushort Page { get; set; }
    public ushort Id { get; set; }
    public HidSharp.Reports.Usage HidSharpUsage { get; }

    public Usage(ushort page, ushort id)
    {
        Page = page;
        Id = id;
        HidSharpUsage = (HidSharp.Reports.Usage)((page << 16) | id);
    }
}

public class Unit
{
    public UnitSystem System { get; set; }
    public int Exponent { get; set; }

    public Unit(UnitSystem system, int exponent)
    {
        System = system;
        Exponent = exponent;
    }
}

public enum UnitSystem
{
    None,
    SILinear,
    SIRotation,
    EnglishLinear,
    EnglishRotation,
    VendorDefined
}
