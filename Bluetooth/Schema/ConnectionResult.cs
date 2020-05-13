namespace Bluetooth.GATT.Schema
{
    public class ConnectionResult
    {
        public string DeviceId { get; set; }
        public string Name { get; set; }
        public bool IsConnected { get; set; }
        public bool? IsPaired { get; set; }
        public bool HasError { get; set; }
        public string ErrorMessage { get; set; }
    }
}
