using System.Management;

namespace Bluetooth.GATT
{
    public static class OSInformation
    {
        public static bool WithWinRT
        {
            get
            {
                string[] version = GetOsVer().Split('.');
                return int.Parse(version[0]) >= 10 && int.Parse(version[2]) >= 17134;
            }
        }
        private static ManagementObject GetMngObj(string className)
        {
            var wmi = new ManagementClass(className);

            foreach (var o in wmi.GetInstances())
            {
                var mo = (ManagementObject)o;
                if (mo != null) return mo;
            }

            return null;
        }

        public static string GetOsVer()
        {
            try
            {
                ManagementObject mo = GetMngObj("Win32_OperatingSystem");
                return mo == null ? string.Empty : mo["Version"] as string;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
