using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

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
                string OSVersion = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
                //Microsoft Windows 10.0.18363
                Regex r = new Regex("^Microsoft Windows ([\\d.]+)");
                var m = r.Match(OSVersion);
                if (m.Success)
                    return new Version(m.Groups[1].Value).CompareTo(new Version(10, 0, 17134)) >= 0;
                return false;
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
