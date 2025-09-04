using HidUps;

var upsDevice = new UpsDevice();
var status = upsDevice.GetDeviceStatus();
Console.WriteLine(status);
