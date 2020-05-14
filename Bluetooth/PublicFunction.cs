using System;
using System.Diagnostics;

namespace Bluetooth.GATT
{
    public static class PublicFunction
    {
        /// <summary>
        /// Is the OS support BLE or not?
        /// OS >= Win 10.17134
        /// </summary>
        public static bool WinRT_API_Support
        {
            get
            {
                var version = Environment.OSVersion;
                return version.Version.Major >= 10 && version.Version.Build >= 17134;
            }
        }
        /// <summary>
        /// Open the BLE setting page from system settings
        /// </summary>
        public static void OpenBLESetting()
        {
            Process.Start("ms-settings-bluetooth:");
        }
    }
}
