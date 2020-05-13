using System;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Bluetooth.GATT
{
    public class PairingHelper
    {
        public static async Task<bool> IsPair(ulong Address)
        {
            var device = await BluetoothLEDevice.FromBluetoothAddressAsync(Address);
            if (device == null)
                return false;
            else
                return device.DeviceInformation.Pairing.IsPaired;
        }
        public static async Task<bool> IsPair(string deviceId)
        {
            var device = await BluetoothLEDevice.FromIdAsync(deviceId);
            if (device == null)
                return false;
            else
                return device.DeviceInformation.Pairing.IsPaired;
        }
        public static async Task<Schema.PairingResult> PairDeviceAsync(ulong Address)
        {
            var device = await BluetoothLEDevice.FromBluetoothAddressAsync(Address);
            if (device != null)
            {
                device.DeviceInformation.Pairing.Custom.PairingRequested += (DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args) => args.Accept();
                var result = await device.DeviceInformation.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmOnly);

                return new Schema.PairingResult()
                {
                    Status = result.Status.ToString()
                };
            }
            else
            {
                return new Schema.PairingResult()
                {
                    Status = string.Format("Device address:{0} not found", Address)
                };
            }
        }
        public static async Task<Schema.PairingResult> PairDeviceAsync(string deviceId)
        {
            var device = await BluetoothLEDevice.FromIdAsync(deviceId);
            if (device != null)
            {
                device.DeviceInformation.Pairing.Custom.PairingRequested += (DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args) => args.Accept();
                var result = await device.DeviceInformation.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmOnly);

                return new Schema.PairingResult()
                {
                    Status = result.Status.ToString()
                };
            }
            else
            {
                return new Schema.PairingResult()
                {
                    Status = string.Format("Device Id:{0} not found", deviceId)
                };
            }
        }

        public static async Task<Schema.PairingResult> UnpairDeviceAsync(string deviceId)
        {
            var device = await BluetoothLEDevice.FromIdAsync(deviceId);
            if (device != null)
            {
                var result = await device.DeviceInformation.Pairing.UnpairAsync();
                return new Schema.PairingResult()
                {
                    Status = result.Status.ToString()
                };
            }
            else
            {
                return new Schema.PairingResult()
                {
                    Status = string.Format("Device Id:{0} not found", deviceId)
                };
            }
        }
        public static async Task<Schema.PairingResult> UnpairDeviceAsync(ulong Address)
        {
            var device = await BluetoothLEDevice.FromBluetoothAddressAsync(Address);
            if (device != null)
            {
                var result = await device.DeviceInformation.Pairing.UnpairAsync();
                return new Schema.PairingResult()
                {
                    Status = result.Status.ToString()
                };
            }
            else
            {
                return new Schema.PairingResult()
                {
                    Status = string.Format("Device address:{0} not found", Address)
                };
            }
        }
    }
}
