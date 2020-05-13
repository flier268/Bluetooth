using System;

namespace Bluetooth.GATT.Events
{
    public class RateChangedEventArgs : EventArgs
    {
        public int BeatsPerMinute { get; set; }
    }
}
