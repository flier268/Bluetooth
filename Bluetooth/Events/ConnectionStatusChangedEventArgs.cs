using System;

namespace Bluetooth.GATT.Events
{
    public class ConnectionStatusChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
    }
}
