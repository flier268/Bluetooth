using System.Collections.Generic;
using Windows.Devices.Enumeration;

namespace Bluetooth.GATT.Schema
{
    public class WatcherDevice
    {
        public string Id { get; set; }
        public ulong Address { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsDefault { get; set; }
        public string Name { get; set; }
        public bool IsPaired { get; set; }
        public bool IsConnected { get; set; }
        public string Kind { get; set; }
        public Dictionary<string, object> Properties { get; set; }
        public DeviceThumbnail DeviceThumbnail { get; set; }
        public int SignalStrength { get; set; }
    }
}
