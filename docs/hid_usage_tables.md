# HID Usage Tables (Version 1.6)

This document contains excerpts from the "HID Usage Tables FOR Universal Serial Bus (USB) VERSION 1.6".

## Usage Page Summary

| **Page ID** | **Page Name** | **Section or Document** |
| --- | --- | --- |
| 00  | Undefined |     |
| 01  | Generic Desktop Page (0x01) | 4   |
| 02  | Simulation Controls Page (0x02) | 5   |
| 03  | VR Controls Page (0x03) | 6   |
| 04  | Sport Controls Page (0x04) | 7   |
| 05  | Game Controls Page (0x05) | 8   |
| 06  | Generic Device Controls Page (0x06) | 9   |
| 07  | Keyboard/Keypad Page (0x07) | 10  |
| 08  | LED Page (0x08) | 11  |
| 09  | Button Page (0x09) | 12  |
| 0A  | Ordinal Page (0x0A) | 13  |
| 0B  | Telephony Device Page (0x0B) | 14  |
| 0C  | Consumer Page (0x0C) | 15  |
| 0D  | Digitizers Page (0x0D) | 16  |
| 0E  | Haptics Page (0x0E) | 17  |
| 0F  | Physical Input Device Page (0x0F) | 18  |
| 10  | Unicode Page (0x10) | 19  |
| 11  | SoC Page (0x11) | 20  |
| 12  | Eye and Head Trackers Page (0x12) | 21  |
| 13-13 | Reserved |     |
| 14  | Auxiliary Display Page (0x14) | 22  |
| 15-1F | Reserved |     |
| 20  | Sensors Page (0x20) | 23  |
| 21-3F | Reserved |     |
| 40  | Medical Instrument Page (0x40) | 24  |
| 41  | Braille Display Page (0x41) | 25  |
| 42-58 | Reserved |     |
| 59  | Lighting And Illumination Page (0x59) | 26  |
| 5A-7F | Reserved |     |
| 80  | Monitor Page (0x80) | 27  |
| 81  | Monitor Enumerated Page (0x81) | 28  |
| 82  | VESA Virtual Controls Page (0x82) | 29  |
| 83-83 | Reserved |     |
| 84  | **Power Page (0x84)** | 30  |
| 85  | **Battery System Page (0x85)** | 31  |
| 86-8B | Reserved |     |
| 8C  | Barcode Scanner Page (0x8C) | 32  |
| 8D  | Scales Page (0x8D) | 33  |
| 8E  | Magnetic Stripe Reader Page (0x8E) | 34  |
| 8F-8F | Reserved |     |
| 90  | Camera Control Page (0x90) | 35  |
| 91  | Arcade Page (0x91) | 36  |
| 92  | Gaming Device Page (0x92) | 37  |
| 93-F1CF | Reserved |     |
| F1D0 | FIDO Alliance Page (0xF1D0) | 38  |
| F1D1-FEFF | Reserved |     |
| FF00-FFFF | Vendor-defined |     |

## Power Page (0x84)

| **Usage ID** | **Usage Name** | **Usage Types** | **Section** |
| --- | --- | --- | --- |
| 00  | Undefined |     |     |
| 01  | iName | SV  | 30.4 |
| 02  | Present Status | CL  | 30.4 |
| 03  | Changed Status | CL  | 30.4 |
| 04  | UPS | CA  | 30.4 |
| 05  | Power Supply | CA  | 30.4 |
| 06-0F | Reserved |     |     |
| 10  | Battery System | CP  | 30.4 |
| 11  | Battery System Id | SV  | 30.4 |
| 12  | Battery | CP  | 30.4 |
| 13  | Battery Id | SV  | 30.4 |
| 14  | Charger | CP  | 30.4 |
| 15  | Charger Id | SV  | 30.4 |
| 16  | Power Converter | CP  | 30.4 |
| 17  | Power Converter Id | SV  | 30.4 |
| 18  | Outlet System | CP  | 30.4 |
| 19  | Outlet System Id | SV  | 30.4 |
| 1A  | Input | CP  | 30.4 |
| 1B  | Input Id | SV  | 30.4 |
| 1C  | Output | CP  | 30.4 |
| 1D  | Output Id | SV  | 30.4 |
| 1E  | Flow | CP  | 30.4 |
| 1F  | Flow Id | SV  | 30.4 |
| 20  | Outlet | CP  | 30.4 |
| 21  | Outlet Id | SV  | 30.4 |
| 22  | Gang | CL/CP | 30.4 |
| 23  | Gang Id | SV  | 30.4 |
| 24  | Power Summary | CL/CP | 30.4 |
| 25  | Power Summary Id | SV  | 30.4 |
| 26-2F | Reserved |     |     |
| 30  | Voltage | DV  | 30.5 |
| 31  | Current | DV  | 30.5 |
| 32  | Frequency | DV  | 30.5 |
| 33  | Apparent Power | DV  | 30.5 |
| 34  | Active Power | DV  | 30.5 |
| 35  | Percent Load | DV  | 30.5 |
| 36  | Temperature | DV  | 30.5 |
| 37  | Humidity | DV  | 30.5 |
| 38  | Bad Count | DV  | 30.5 |
| 39-3F | Reserved |     |     |
| 40  | Config Voltage | SV/DV | 30.6 |
| 41  | Config Current | SV/DV | 30.6 |
| 42  | Config Frequency | SV/DV | 30.6 |
| 43  | Config Apparent Power | SV/DV | 30.6 |
| 44  | Config Active Power | SV/DV | 30.6 |
| 45  | Config Percent Load | SV/DV | 30.6 |
| 46  | Config Temperature | SV/DV | 30.6 |
| 47  | Config Humidity | SV/DV | 30.6 |
| 48-4F | Reserved |     |     |
| 50  | Switch On Control | DV  | 30.7 |
| 51  | Switch Off Control | DV  | 30.7 |
| 52  | Toggle Control | DV  | 30.7 |
| 53  | Low Voltage Transfer | DV  | 30.7 |
| 54  | High Voltage Transfer | DV  | 30.7 |
| 55  | Delay Before Reboot | DV  | 30.7 |
| 56  | Delay Before Startup | DV  | 30.7 |
| 57  | Delay Before Shutdown | DV  | 30.7 |
| 58  | Test | DV  | 30.7 |
| 59  | Module Reset | DV  | 30.7 |
| 5A  | Audible Alarm Control | DV  | 30.7 |
| 5B-5F | Reserved |     |     |
| 60  | Present | DF  | 30.8 |
| 61  | Good | DF  | 30.8 |
| 62  | Internal Failure | DF  | 30.8 |
| 63  | Voltage Out Of Range | DF  | 30.8 |
| 64  | Frequency Out Of Range | DF  | 30.8 |
| 65  | Overload | DF  | 30.8 |
| 66  | Over Charged | DF  | 30.8 |
| 67  | Over Temperature | DF  | 30.8 |
| 68  | Shutdown Requested | DF  | 30.8 |
| 69  | Shutdown Imminent | DF  | 30.8 |
| 6A-6A | Reserved |     |     |
| 6B  | Switch On/Off | DF  | 30.8 |
| 6C  | Switchable | DF  | 30.8 |
| 6D  | Used | DF  | 30.8 |
| 6E  | Boost | DF  | 30.8 |
| 6F  | Buck | DF  | 30.8 |
| 70  | Initialized | DF  | 30.8 |
| 71  | Tested | DF  | 30.8 |
| 72  | Awaiting Power | DF  | 30.8 |
| 73  | Communication Lost | DF  | 30.8 |
| 74-FC | Reserved |     |     |
| FD  | iManufacturer | SV  | 30.9 |
| FE  | iProduct | SV  | 30.9 |
| FF  | iSerialNumber | SV  | 30.9 |
| 100-FFFF | Reserved |     |     |
| --- | --- | --- | --- |

## Battery System Page (0x85)

| **Usage ID** | **Usage Name** | **Usage Types** | **Section** |
| --- | --- | --- | --- |
| 00  | Undefined |     |     |
| 01  | Smart Battery Battery Mode | CL  | 31.2 |
| 02  | Smart Battery Battery Status | NAry | 31.3.1 |
| 03  | Smart Battery Alarm Warning | NAry | 31.3.2 |
| 04  | Smart Battery Charger Mode | CL  | 31.6 |
| 05  | Smart Battery Charger Status | CL  | 31.7 |
| 06  | Smart Battery Charger Spec Info | CL  | 31.8 |
| 07  | Smart Battery Selector State | CL  | 31.1.1 |
| 08  | Smart Battery Selector Presets | CL  | 31.1.2 |
| 09  | Smart Battery Selector Info | CL  | 31.1.3 |
| 0A-0F | Reserved |     |     |
| 10  | Optional Mfg Function 1 | DV  | 31.1 |
| 11  | Optional Mfg Function 2 | DV  | 31.1 |
| 12  | Optional Mfg Function 3 | DV  | 31.1 |
| 13  | Optional Mfg Function 4 | DV  | 31.1 |
| 14  | Optional Mfg Function 5 | DV  | 31.1 |
| 15  | Connection To SM Bus | DF  | 31.1.1 |
| 16  | Output Connection | DF  | 31.1.1 |
| 17  | Charger Connection | DF  | 31.1.1 |
| 18  | Battery Insertion | DF  | 31.1.1 |
| 19  | Use Next | DF  | 31.1.2 |
| 1A  | OK To Use | DF  | 31.1.2 |
| 1B  | Battery Supported | DF  | 31.1.3 |
| 1C  | Selector Revision | DF  | 31.1.3 |
| 1D  | Charging Indicator | DF  | 31.1.3 |
| 1E-27 | Reserved |     |     |
| 28  | Manufacturer Access | DV  | 31.2 |
| 29  | Remaining Capacity Limit | DV  | 31.2 |
| 2A  | Remaining Time Limit | DV  | 31.2 |
| 2B  | At Rate | DV  | 31.2 |
| 2C  | Capacity Mode | DV  | 31.2 |
| 2D  | Broadcast To Charger | DV  | 31.2 |
| 2E  | Primary Battery | DV  | 31.2 |
| 2F  | Charge Controller | DV  | 31.2 |
| 30-3F | Reserved |     |     |
| 40  | Terminate Charge | Sel | 31.3.2 |
| 41  | Terminate Discharge | Sel | 31.3.2 |
| 42  | Below Remaining Capacity Limit | Sel | 31.3.2 |
| 43  | Remaining Time Limit Expired | Sel | 31.3.2 |
| 44  | Charging | Sel | 31.3.1 |
| 45  | Discharging | Sel | 31.3.1 |
| 46  | Fully Charged | Sel | 31.3.1 |
| 47  | Fully Discharged | Sel | 31.3.1 |
| 48  | Conditioning Flag | DF  | 31.3 |
| 49  | At Rate OK | DF  | 31.3 |
| 4A  | Smart Battery Error Code | DV  | 31.3 |
| 4B  | Need Replacement | DF  | 31.3 |
| 4C-5F | Reserved |     |     |
| 60  | At Rate Time To Full | DV  | 31.4 |
| 61  | At Rate Time To Empty | DV  | 31.4 |
| 62  | Average Current | DV  | 31.4 |
| 63  | Max Error | DV  | 31.4 |
| 64  | Relative State Of Charge | DV  | 31.4 |
| 65  | Absolute State Of Charge | DV  | 31.4 |
| 66  | Remaining Capacity | DV  | 31.4 |
| 67  | Full Charge Capacity | DV  | 31.4 |
| 68  | Run Time To Empty | DV  | 31.4 |
| 69  | Average Time To Empty | DV  | 31.4 |
| 6A  | Average Time To Full | DV  | 31.4 |
| 6B  | Cycle Count | DV  | 31.4 |
| 6C-7F | Reserved |     |     |
| 80  | Battery Pack Model Level | SV  | 31.5 |
| 81  | Internal Charge Controller | SF  | 31.5 |
| 82  | Primary Battery Support | SF  | 31.5 |
| 83  | Design Capacity | SV  | 31.5 |
| 84  | Specification Info | SV  | 31.5 |
| 85  | Manufacture Date | SV  | 31.5 |
| 86  | Serial Number | SV  | 31.5 |
| 87  | iManufacturer Name | SV  | 31.5 |
| 88  | iDevice Name | SV  | 31.5 |
| 89  | iDevice Chemistry | SV  | 31.5 |
| 8A  | Manufacturer Data | SV  | 31.5 |
| 8B  | Rechargable | SV  | 31.5 |
| 8C  | Warning Capacity Limit | SV  | 31.5 |
| 8D  | Capacity Granularity 1 | SV  | 31.5 |
| 8E  | Capacity Granularity 2 | SV  | 31.5 |
| 8F  | OEM Information | SV  | 31.5 |
| 90-BF | Reserved |     |     |
| C0  | Inhibit Charge | DF  | 31.6 |
| C1  | Enable Polling | DF  | 31.6 |
| C2  | Reset To Zero | DF  | 31.6 |
| C3-CF | Reserved |     |     |
| D0  | AC Present | DV  | 31.7 |
| D1  | Battery Present | DV  | 31.7 |
| D2  | Power Fail | DV  | 31.7 |
| D3  | Alarm Inhibited | DV  | 31.7 |
| D4  | Thermistor Under Range | DV  | 31.7 |
| D5  | Thermistor Hot | DV  | 31.7 |
| D6  | Thermistor Cold | DV  | 31.7 |
| D7  | Thermistor Over Range | DV  | 31.7 |
| D8  | Voltage Out Of Range | DV  | 31.7 |
| D9  | Current Out Of Range | DV  | 31.7 |
| DA  | Current Not Regulated | DV  | 31.7 |
| DB  | Voltage Not Regulated | DV  | 31.7 |
| DC  | Master Mode | DV  | 31.7 |
| DD-EF | Reserved |     |     |
| F0  | Charger Selector Support | SF  | 31.8 |
| F1  | Charger Spec | SV  | 31.8 |
| F2  | Level 2 | SF  | 31.8 |
| F3  | Level 3 | SF  | 31.8 |