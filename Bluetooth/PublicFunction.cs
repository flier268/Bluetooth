using System.Management;

namespace Bluetooth.GATT
{
    public class PublicFunction
    {
        public static bool WinRT_API_Support
        {
            get
            {
                string[] version = GetOsVer().Split('.');
                if (int.Parse(version[0]) >= 10 && int.Parse(version[2]) >= 17134)
                    return true;
                else
                    return false;
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

                if (null == mo)
                    return string.Empty;

                return mo["Version"] as string;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
