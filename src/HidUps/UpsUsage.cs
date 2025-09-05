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
        PowerSummaryCollection = 0x00840024,
        BatterySystemCollection = 0x00840010,
        BatteryCollection = 0x00840012,
        PowerConverterCollection = 0x00840016,
        InputCollection = 0x0084001A,
        OutputCollection = 0x0084001C,
        FlowCollection = 0x0084001E,
        OutletSystemCollection = 0x00840018,
        
        // -- PresentStatus & Status Usages --
        PresentStatus = 0x00840002,
        ChangedStatus = 0x00840003,
        Good = 0x00840061,
        InternalFailure = 0x00840062,
        VoltageOutOfRange = 0x00840063,
        FrequencyOutOfRange = 0x00840064,
        Overload = 0x00840065,
        OverCharged = 0x00840066,
        OverTemperature = 0x00840067,
        ShutdownRequested = 0x00840068,
        ShutdownImminent = 0x00840069,
        SwitchOnOff = 0x0084006B,
        Switchable = 0x0084006C,
        Used = 0x0084006D,
        Boost = 0x0084006E,
        Buck = 0x0084006F,
        Initialized = 0x00840070,
        Tested = 0x00840071,
        AwaitingPower = 0x00840072,
        CommunicationLost = 0x00840073,

        // -- Measurement Usages --
        Voltage = 0x00840030,
        Current = 0x00840031,
        Frequency = 0x00840032,
        ApparentPower = 0x00840033,
        ActivePower = 0x00840034,
        PercentLoad = 0x00840035,
        Temperature = 0x00840036,
        Humidity = 0x00840037,
        BadCount = 0x00840038,

        // -- Configuration & Control Usages --
        ConfigVoltage = 0x00840040,
        ConfigCurrent = 0x00840041,
        ConfigFrequency = 0x00840042,
        ConfigApparentPower = 0x00840043,
        ConfigActivePower = 0x00840044,
        ConfigPercentLoad = 0x00840045,
        SwitchOnControl = 0x00840050,
        SwitchOffControl = 0x00840051,
        ToggleControl = 0x00840052,
        LowVoltageTransfer = 0x00840053,
        HighVoltageTransfer = 0x00840054,
        DelayBeforeReboot = 0x00840055,
        DelayBeforeStartup = 0x00840056,
        DelayBeforeShutdown = 0x00840057,
        Test = 0x00840058,
        ModuleReset = 0x00840059,
        AudibleAlarmControl = 0x0084005A,

        // Battery System Page (0x85)
        BatterySystemPage = 0x00850000,
        RemainingCapacityLimit = 0x00850029,
        RemainingTimeLimit = 0x0085002A,
        CapacityMode = 0x0085002C,
        TerminateCharge = 0x00850040,
        TerminateDischarge = 0x00850041,
        BelowRemainingCapacityLimit = 0x00850042,
        RemainingTimeLimitExpired = 0x00850043,
        Charging = 0x00850044,
        Discharging = 0x00850045,
        FullyCharged = 0x00850046,
        FullyDischarged = 0x00850047,
        NeedReplacement = 0x0085004B,
        AtRateTimeToFull = 0x00850060,
        AtRateTimeToEmpty = 0x00850061,
        RelativeStateOfCharge = 0x00850064,
        AbsoluteStateOfCharge = 0x00850065,
        RemainingCapacity = 0x00850066,
        FullChargeCapacity = 0x00850067,
        RunTimeToEmpty = 0x00850068,
        AverageTimeToEmpty = 0x00850069,
        AverageTimeToFull = 0x0085006A,
        CycleCount = 0x0085006B,
        DesignCapacity = 0x00850083,
        DeviceChemistry = 0x00850089,
        Rechargeable = 0x0085008B,
        WarningCapacityLimit = 0x0085008C,
        CapacityGranularity1 = 0x0085008D,
        CapacityGranularity2 = 0x0085008E,
        OemInformation = 0x0085008F,
        AcPresent = 0x008500D0, // Often in PowerSummary bitfield
        BatteryPresent = 0x008500D1,

        // Vendor Specific (0xFFFF) - Tripplite specific
        VendorPage = 0xFFFF0000,
        AutoRestartAfterShutdown = 0xFFFF00C7,
        OutputSource = 0xFFFF0091,
        SiteWiringFault = 0xFFFF0093,
    }
}
