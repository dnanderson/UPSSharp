# Tripplite Communication Protocol 2012

Created on: 2/03/2011

Revision: 1.0

### Legend

1. **Items in black are supported.**
2. **Items in blue are supported but are user defined, these are Tripplite specific.**
3. **Items in red are not supported.**

## Report Descriptor Skeleton

- UPS application collection
  - PowerSummary physical collection
    - AC input collection
    - End AC input collection
    - PresentStatus logical collection
    - End PresentStatus logical collection
  - End PowerSummary physical collection
  - BatterySystem physical collection
    - Battery physical collection
      - PresentStatus logical collection
      - End PresentStatus logical collection
    - End battery physical collection
  - End BatterySystem physical collection
  - Flow physical collection
  - End flow physical collection
  - PowerConverter physical collection
    - PresentStatus logical collection
    - End PresentStatus logical collection
  - End PowerConverter physical collection
  - OutletSystem physical collection
    - Outlet physical collection
      - PresentStatus logical collection
      - End PresentStatus logical collection
    - End outlet physical collection
    - Outlet physical collection
      - PresentStatus logical collection
      - End PresentStatus logical collection
    - End outlet physical collection
  - End OutletSystem physical collection
  - Vender defined collection
  - End vender defined collection
- End UPS application collection

## 1\. Tripp Lite USB Report Descriptor Protocol

This protocol is intended to describe the protocol layer between the UPS USB Report Descriptor and the Application Software. Instead of the formal way of creating a UPS Report Descriptor, the point of view of a Power Device structure will be used to create a Report Descriptor for a USB based UPS.

A UPS Report Descriptor includes six items: "Power Configuration Controls", "Power Controls", "Power Generic Status", "Power Device Identification", "Power Measures", and "Battery System". Each item shares a corresponding Report ID, and each Report ID field is pre-defined (Order, Byte #, Unit in each Report ID).

## 2\. TrippLite Protocol (Tripp Lite Serial Report Descriptor Protocol)

The purpose of this protocol is to transfer the Report Descriptor from USB to serial communication so that the UPS can be built with two communication ports (USB & RS232) with less effort. This protocol reduces development time and saves limited RAM.

The PC end communicates with the UPS continually, meaning it won't wait 1 second for each frame anymore.

### RS232 Configuration

- **Baud:** 2400
- **Data:** 8 bits
- **Parity:** None
- **Start Bit:** 1

### Message Format (Binary)

| **Header** | **Type** | **Length** | **Report ID** | **Data** | **Check Sum** |
| --- | --- | --- | --- | --- | --- |
| 1 byte | 1 byte | 1 byte | 1 byte | 64 bytes max | 1 byte |
| --- | --- | --- | --- | --- | --- |

#### 2.1 Header

The header will be a ~ character ( 0x7E in hex).

#### 2.2 Type

| **Value** | **Description** | **Direction** |
| --- | --- | --- |
| 0x01 | Command rejected | UPS → Computer |
| --- | --- | --- |
| 0x02 | Command accepted | UPS → Computer |
| --- | --- | --- |
| 0x03 | Polling command | Computer → UPS |
| --- | --- | --- |
| 0x04 | Set command | Computer → UPS |
| --- | --- | --- |
| 0x05 | Data returned | UPS → Computer |
| --- | --- | --- |

#### 2.3 Length

The length is the number of bytes from the "Report ID" to the "Data" items.

#### 2.4 Report ID

Identifies which item the software inquires.

#### 2.5 Data

2.5.1 Polling Commands:

PC->UPS (Inquire Input Voltage)

0x7E, 0x03, 0x02, 0x18, 0x00, 0x9B

UPS->PC (return 120V)

0x7E, 0x05, 0x03, 0x18, Lo=0xB0, Hi=0x04, 0x52

2.5.2 Set Commands:

PC->UPS (Set Shutdown Time 60 seconds)

0x7E, 0x04, 0x03, 0x15, Lo=0x3C, Hi=0x00, 0xD6

UPS->PC

0x7E, 0x02, 0x03, 0x15, Lo=0x3C, Hi=0x00, 0xD4

#### 2.6 Check Sum

Sum the bytes from "Header" to "Data". Overflow is ignored.

## 3\. Tripplite Communication Protocol Collections

### Power Summary Collection

| **Report ID #** | **Usage (usage page : usage ID)** | **Byte #** | **Unit** | **Type** | **R/W** | **Supported?** |
| --- | --- | --- | --- | --- | --- | --- |
| 40  | iProduct (0x84:0xfe) | 1   |     | Feature | R   | Y   |
| 41  | iSerialNumber (0x84:0xff) | 1   |     | Feature | R   | Y   |
| 43  | iManufacture (0x84:0xfd) | 1   |     | Feature | R   | Y   |
| 48  | ConfigVoltage(Input Line) (0x84:0x40) | 1   | 1V  | Feature | R   | Y   |
| 49  | Voltage(Input Line) (0x84:0x30) | 2   | 0.1V | Feature | R   | Y   |
| 17  | AudibleAlarmControl (0x84:0x5a) | 1   |     | Feature | R/W | Y   |

- **iProduct:** Index of a string descriptor describing the product. For RS232, the string is reported directly.
- **iSerialNumber:** Index of a string descriptor for the device's serial number. Also used for firmware part number (e.g., "FW-2263 A").
- **iManufacture:** Index of a string descriptor describing the manufacturer.
- **Config Voltage:** Nominal value of the input line voltage.
- **Voltage:** Value of the input line voltage.
- **AudibleAlarm Control:** 1: Disable, 2: Enable, 3: Temporary Mute (not supported).

### Battery Information (within Power Summary)

| **Report ID #** | **Usage (usage page : usage ID)** | **Byte #** | **Unit** | **Type** | **R/W** | **Supported?** |
| --- | --- | --- | --- | --- | --- | --- |
| 50  | PresentStatus (0x84:0x02) - Bit field | 1   |     | Feature & Input | R   | Y   |
| 42  | iDevice Chemistry (0x85:0x89) | 1   |     | Feature | R   | Y   |
| 98  | iOEMInformation (0x85:0x8f) | 1   |     | Feature | R   | N   |
| 51  | CapacityMode (0x85:0x2c) | 1   |     | Feature | R   | Y   |
| 52  | RemainingCapacity (0x85:0x66) | 1   |     | Feature & Input | R   | Y   |
| 58  | RemainingCapacityLimit (0x85:0x29) | 1   |     | Feature | R   | N   |
| 55  | FullCharge Capacity (0x85:0x67) | 1   |     | Feature | R   | Y   |
| 54  | DesignCapacity (0x85:0x83) | 1   |     | Feature | R   | Y   |
| 56  | WarningCapacityLimit (0x85:0x8c) | 1   |     | Feature | R   | N   |
| 59  | CapacityGranularity1 (0x85:0x8d) | 1   |     | Feature | R   | N   |
| 57  | CapacityGranularity2 (0x85:0x8e) | 1   |     | Feature | R   | N   |
| 44  | Rechargeable (0x85:0x8b) | 1   |     | Feature | R   | N   |
| 53  | Run TimeToEmpty (0x85:0x68) | 2   | Sec | Feature & Input | R   | Y   |

- **PresentStatus Bits:**
  - 0 ShutdownImminent: Reports 1 when unit is at Low Battery.
  - 1 ACPresent: Set if AC input is VALID.
  - 2 Charging: Set whenever the charger is running (even in float mode).
  - 3 Discharging: Set whenever the unit is in invert mode and not charging.
  - 4 Need Replacement: Set if the unit failed a Selftest due to a low battery.
  - 5 BelowRemainingCapacityLimit: Handled same as ShutdownImminent.
  - 6 FullyCharged: Set if RemainingCapacity is 100.
  - 7 Fully Discharged: Always read as 0.
- **CapacityMode:** 0=maH, 1=mwH, 2=%, 3=Boolean. Normally returns 2 (percentage).
- **FullChargeCapacity:** Predicted capacity when fully charged (100).
- **DesignCapacity:** Theoretical capacity of a new pack (100).
- **Run TimeToEmpty:** Predicted remaining battery life at the current discharge rate.

### BatterySystem Collection

| **Report ID #** | **Usage (usage page : usage ID)** | **Byte #** | **Unit** | **Type** | **R/W** | **Supported?** |
| --- | --- | --- | --- | --- | --- | --- |
| 4   | ConfigVoltage(Battery) (0x84:0x40) | 2   | 1V  | Feature | R   | Y   |
| 32  | Voltage(Battery) (0x84:0x30) | 2   | 0.1V | Feature | R   | Y   |
| 35  | Battery PresentStatus (0x84:0x02) | 1   |     | Feature | R   | Y   |
| 33  | RemainingCapacity (0x85:0x66) | 1   | %   | Feature | R   | N   |
| 16  | Test (0x84:0x58) | 1   |     | Feature | R/W | Y   |
| 26  | Temperature (0x84:0x36) | 2   | K   | Feature | R   | N   |
| --- | --- | --- | --- | --- | --- | --- |

- **Battery PresentStatus Bits:** 0: Charging, 1: Discharging, 2: NeedReplacement.
- **Test:**
  - **Write value:** 1: Quick test. (Others not supported).
  - **Read value:** 0: No test initiated, 1: Done and Passed, 3: Done and Error, 5: In progress.

### AC Flow Collection

| **Report ID #** | **Usage (usage page : usage ID)** | **Byte #** | **Unit** | **Type** | **R/W** | **Supported?** |
| --- | --- | --- | --- | --- | --- | --- |
| 1   | ConfigVoltage(Input Line) (0x84:0x40) | 1   | V   | Feature | R   | Y   |
| 2   | ConfigFrequency (0x84:0x42) | 1   | Hz  | Feature | R   | Y   |
| 5   | ConfigPercentLoad (0x84:0x45) | 1   | %   | Feature | R   | N   |
| 3   | ConfigApparentPower (0x84:0x43) | 2   | VA  | Feature | R   | Y   |
| 85  | SiteWiringFault (0xffff:0x93) | 1   |     | Feature | R   | N   |

- **ConfigApparentPower:** Maximum VA capacity of the unit.
- **SiteWiring Fault:** 0: Wiring OK, 1: Wiring Fault.

### PowerConverter Collection

| **Report ID #** | **Usage (usage page : usage ID)** | **Byte #** | **Unit** | **Type** | **R/W** | **Supported?** |
| --- | --- | --- | --- | --- | --- | --- |
| 24  | (Input) Voltage (0x84:0x30) | 2   | 0.1V | Feature | R   | Y   |
| 25  | (Input) Frequency (0x84:0x32) | 2   | 0.1Hz | Feature | R   | Y   |
| 27  | (Output) Voltage (0x84:0x30) | 2   | 0.1V | Feature | R   | Y   |
| 28  | (Output) Frequency (0x84:0x32) | 2   | 0.1Hz | Feature | R   | N   |
| 71  | (Output) ActivePower (0x84:0x34) | 2   | SEE | Feature | R   | Y   |
| 6   | LowVoltageTransfer (0x84:0x53) | 2   | V   | Feature | R   | N   |
| 9   | HighVoltageTransfer (0x84:0x54) | 2   | V   | Feature | R   | N   |
| 34  | Power PresentStatus (0x84:0x02) | 2   |     | Feature | R   | Y   |

- **ActivePower:** Units are in Watts. 0xFFFF indicates watts are not supported.
- **Power PresentStatus Bits:**
  - 0 VoltageOutOfRange: Opposite of ACPresent.
  - 1 Buck: Unit is reducing AC input voltage.
  - 2 Boost: Unit is increasing AC input voltage.
  - 4 Overload: Unit is overloaded (>110%) or has shut down due to overload.
  - 5 UPS Off: Unused.
  - 6 OverTemperature: Not supported.
  - 7 InternalFailure: Not supported.
  - 14 AwaitingPower: Always read as 0.

### OutletSystem Collection

| **Report ID #** | **Usage (usage page : usage ID)** | **Byte #** | **Unit** | **Type** | **R/W** | **Supported?** |
| --- | --- | --- | --- | --- | --- | --- |
| 160 | ShutdownRequested (0x84:68) | 1   |     | Feature | R/W | N   |
| 21  | DelayBeforeShutdown (0x84:0x57) | 2   | Sec | Feature | R/W | Y   |
| 182 | AutoRestartAfterSDCmd (0xffff:C7) | 1   |     | Feature | R/W | Y   |
| 22  | DelayBeforeStartup (0xffff:0x56) | 2   | Min | Feature | R/W | N   |
| 23  | DelayBeforeReboot (0x84:0x55) | 2   | Sec | Feature | R/W | Y   |
| 97  | DelayBeforeStartup (0x84:0x56) | 2   | Sec | Feature | R/W | N   |
| 30  | PercentLoad (0x84:0x35) | 1   | SEE | Feature | R   | Y   |
| 65  | PowerOnDelay (0xffff:0x81) | 1   | Sec | Feature | R/W | N   |
| 81  | OutputSource (0xffff:0x91) | 1   |     | Feature | R   | Y   |
| 84  | ActivePower (0x84:0x34) | 2   | W   | Feature | R   | N   |
| 83  | WatchdogAlarm (0xffff:0x93) | 1   |     | Feature | R/W | N   |
| 86  | EmergencyPowerOff (0xffff:0x94) | 1   |     | Feature | R/W | N   |
| --- | --- | --- | --- | --- | --- | --- |

- **DelayBeforeShutdown:** Writing a value shuts down the output after the indicated seconds. 0 is immediate, -1 aborts. Reading returns seconds remaining.
- **AutoRestartAfterSDCmd:** If set to 1, the unit will auto-restart if line power becomes valid after a shutdown command.
- **DelayBeforeReboot:** Shuts down output for the indicated number of seconds.
- **PercentLoad:** Units are in %. 0xFF means data is not valid.
- **OutputSource:** 0=Normal, 1=Battery, 2=Bypass, 3=Reducing, 4=Boosting, 5=Manual Bypass, 6=None.

### Miscellaneous Collection

| **Report ID #** | **Usage (usage page : usage ID)** | **Byte #** | **Unit** | **Type** | **R/W** | **Supported?** |
| --- | --- | --- | --- | --- | --- | --- |
| 13  | iModelString (0xffff:0x75) | 1   |     | Feature | R   | N   |
| 14  | iModelStringOffset (0xffff:0x76) | 1   |     | Feature | R   | N   |
| 15  | UpsType (0xffff:0x7c) | 1   |     | Feature | R   | N   |
| 108 | CommProtocol (0xffff:7d) | 2   | BCD | Feature | R   | Y   |
| --- | --- | --- | --- | --- | --- | --- |

- **UPSType:**
  - **Low 4 bits:** 0: On-Line, 1: Off-Line, 2: Line-Interactive, etc.
  - **High 4 bits:** Indicates firmware version.
- **CommProtocol:** Numeric value indicating the communication protocol. This protocol number is 0x2012.