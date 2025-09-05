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

        // -- Collection Usages --
        PowerSummaryCollection = 0x0084000D,  // Important for Tripplite
        BatterySystemCollection = 0x00840010, // Important for Tripplite
        BatteryCollection = 0x00840012,
        InputCollection = 0x0084001A,
        FlowCollection = 0x0084001E,          // AC Flow in Tripplite
        OutputCollection = 0x0084001C,
        OutletSystemCollection = 0x00840020,  // Important for Tripplite
        PowerConverterCollection = 0x00840016, // Important for Tripplite

        // -- PresentStatus Usages (0x84:0x02 bitfield) --
        PresentStatus = 0x00840002,

        // -- Measurement Usages --
        Voltage = 0x00840030,
        Frequency = 0x00840032,
        ApparentPower = 0x00840033,
        ActivePower = 0x00840034,
        PercentLoad = 0x00840035,
        Temperature = 0x00840036,
        ConfigVoltage = 0x00840040,
        ConfigFrequency = 0x00840042,
        ConfigApparentPower = 0x00840043,
        LowVoltageTransfer = 0x00840053,
        HighVoltageTransfer = 0x00840054,
        DelayBeforeReboot = 0x00840055,
        DelayBeforeStartup = 0x00840056,
        DelayBeforeShutdown = 0x00840057,
        Test = 0x00840058,
        AudibleAlarmControl = 0x0084005A,
        InternalFailure = 0x00840062,
        VoltageOutOfRange = 0x00840063,
        Overload = 0x00840065,
        OverTemperature = 0x00840067,
        ShutdownRequested = 0x00840068,
        ShutdownImminent = 0x00840069,
        UpsOff = 0x0084006D,
        Boost = 0x0084006E,
        Buck = 0x0084006F,
        AwaitingPower = 0x00840072,

        // Battery System Page (0x85)
        BatterySystemPage = 0x00850000,
        RemainingCapacityLimit = 0x00850029,
        CapacityMode = 0x0085002C,
        BelowRemainingCapacityLimit = 0x00850042,
        Charging = 0x00850044,
        Discharging = 0x00850045,
        FullyCharged = 0x00850046,
        FullyDischarged = 0x00850047,
        NeedReplacement = 0x0085004B,
        RemainingCapacity = 0x00850066,
        FullChargeCapacity = 0x00850067,
        RunTimeToEmpty = 0x00850068,
        DesignCapacity = 0x00850083,
        DeviceChemistry = 0x00850089,
        Rechargeable = 0x0085008B,
        WarningCapacityLimit = 0x0085008C,
        CapacityGranularity1 = 0x0085008D,
        CapacityGranularity2 = 0x0085008E,
        OemInformation = 0x0085008F,
        AcPresent = 0x008500D0,

        // Vendor Specific (0xFFFF) - Tripplite specific
        VendorPage = 0xFFFF0000,
        AutoRestartAfterShutdown = 0xFFFF00C7,
        OutputSource = 0xFFFF0091,
        SiteWiringFault = 0xFFFF0093,
    }
}