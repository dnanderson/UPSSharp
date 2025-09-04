namespace HidUps
{
    /// <summary>
    /// Defines standard HID Usages for UPS devices based on the USB HID Usage Tables.
    /// The value is a combination of the Usage Page (high 16 bits) and Usage ID (low 16 bits).
    /// </summary>
    public enum UpsUsage : uint
    {
        // Power Device Page (0x84)
        PowerDevicePage = 0x00840000,
        Ups = 0x00840004,
        PowerSupply = 0x00840005,
        Voltage = 0x00840030,
        Frequency = 0x00840032,
        ApparentPower = 0x00840033,
        ActivePower = 0x00840034,
        PercentLoad = 0x00840035,
        DelayBeforeShutdown = 0x00840057,
        ShutdownImminent = 0x00840069,
        Overload = 0x00840065,

        // Battery System Page (0x85)
        BatterySystemPage = 0x00850000,
        RemainingCapacity = 0x00850066,
        FullChargeCapacity = 0x00850067,
        RunTimeToEmpty = 0x00850068,
        RemainingCapacityLimit = 0x00850029,
        Charging = 0x00850044,
        Discharging = 0x00850045,
        FullyCharged = 0x00850046,
        NeedReplacement = 0x0085004B,
        AcPresent = 0x008500D0,
    }
}
