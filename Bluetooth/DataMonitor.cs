using Bluetooth.GATT.Events;
using Bluetooth.GATT.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;

namespace Bluetooth.GATT
{
    public class DataMonitor : IDisposable
    {
        private BluetoothLEDevice _GATTDevice = null;
        private readonly List<BluetoothAttribute> _serviceCollection = new List<BluetoothAttribute>();

        private BluetoothAttribute _GATTMeasurementAttribute;
        private BluetoothAttribute _GATTAttribute;
        private GattCharacteristic _GATTMeasurementCharacteristic;

        public delegate void DataReceivedDelegate(byte[] byteArray);
        public event DataReceivedDelegate OnDataReceived;
        private readonly SemaphoreSlim lock_Disconnect = new SemaphoreSlim(1);

        /// <summary>
        /// Occurs when [connection status changed].
        /// </summary>
        public event EventHandler<ConnectionStatusChangedEventArgs> ConnectionStatusChanged;
        /// <summary>
        /// Raises the <see cref="E:ConnectionStatusChanged" /> event.
        /// </summary>
        /// <param name="e">The <see cref="ConnectionStatusChangedEventArgs"/> instance containing the event data.</param>
        protected virtual void OnConnectionStatusChanged(ConnectionStatusChangedEventArgs e)
        {
            ConnectionStatusChanged?.Invoke(this, e);
        }
        public async Task<ConnectionResult> ConnectAsync(string deviceId, string Collection, string characteristicsName, bool TryToPair = false)
        {
            _GATTDevice = await BluetoothLEDevice.FromIdAsync(deviceId);
            if (_GATTDevice == null)
            {
                return new ConnectionResult()
                {
                    IsConnected = false,
                    ErrorMessage = "Could not find specified device"
                };
            }

            // we should always monitor the connection status
            _GATTDevice.ConnectionStatusChanged -= DeviceConnectionStatusChanged;
            _GATTDevice.ConnectionStatusChanged += DeviceConnectionStatusChanged;

            var isReachable = await GetDeviceServicesAsync().ConfigureAwait(false);
            if (!isReachable)
            {
                _GATTDevice = null;
                return new ConnectionResult()
                {
                    IsConnected = false,
                    ErrorMessage = "Device is unreachable (i.e. out of range or shutoff)",
                };
            }

            CharacteristicResult characteristicResult = await SetupGATTCharacteristic(Collection, characteristicsName).ConfigureAwait(false);
            if (!characteristicResult.IsSuccess)
            {
                return new ConnectionResult()
                {
                    IsConnected = false,
                    ErrorMessage = characteristicResult.Message
                };
            }

            // we could force propagation of event with connection status change, to run the callback for initial status
            //DeviceConnectionStatusChanged(_GATTDevice, null);
            if (TryToPair && _GATTDevice.DeviceInformation.Pairing.CanPair && !_GATTDevice.DeviceInformation.Pairing.IsPaired)
            {
                _GATTDevice.DeviceInformation.Pairing.Custom.PairingRequested += (DeviceInformationCustomPairing _, DevicePairingRequestedEventArgs args) => args.Accept();
                await _GATTDevice.DeviceInformation.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmOnly);
            }
            if (_GATTDevice.ConnectionStatus == BluetoothConnectionStatus.Connected)
                ManualResetEventSlim.Set();
            else
                ManualResetEventSlim.Reset();
            return new ConnectionResult()
            {
                IsConnected = _GATTDevice.ConnectionStatus == BluetoothConnectionStatus.Connected,
                Name = _GATTDevice.Name,
                IsPaired = _GATTDevice.DeviceInformation.Pairing.IsPaired
            };
        }

        private async Task<List<BluetoothAttribute>> GetServiceCharacteristicsAsync(BluetoothAttribute service)
        {
            IReadOnlyList<GattCharacteristic> characteristics = null;
            try
            {
                // Ensure we have access to the device.
                var accessStatus = await service.service.RequestAccessAsync();
                if (accessStatus == DeviceAccessStatus.Allowed)
                {
                    // BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characterstics only
                    // and the new Async functions to get the characteristics of unpaired devices as well.
                    var result = await service.service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        characteristics = result.Characteristics;
                    }
                    else
                    {
                        characteristics = new List<GattCharacteristic>();
                    }
                }
                else
                {
                    // Not granted access
                    // On error, act as if there are no characteristics.
                    characteristics = new List<GattCharacteristic>();
                }
            }
            catch
            {
                characteristics = new List<GattCharacteristic>();
            }

            var characteristicCollection = new List<BluetoothAttribute>();
            characteristicCollection.AddRange(characteristics.Select(a => new BluetoothAttribute(a)));
            return characteristicCollection;
        }

        private async Task<CharacteristicResult> SetupGATTCharacteristic(string Collection, string characteristicsName)
        {
            _GATTAttribute = _serviceCollection.Find(a => a.Name == Collection);
            if (_GATTAttribute == null)
            {
                return new CharacteristicResult()
                {
                    IsSuccess = false,
                    Message = string.Format("Cannot find {0} service", Collection)
                };
            }

            var characteristics = await GetServiceCharacteristicsAsync(_GATTAttribute).ConfigureAwait(false);
            _GATTMeasurementAttribute = characteristics.Find(a => a.Name == characteristicsName);
            if (_GATTMeasurementAttribute == null)
            {
                return new CharacteristicResult()
                {
                    IsSuccess = false,
                    Message = string.Format("Cannot find {0} characteristic", characteristicsName)
                };
            }
            _GATTMeasurementCharacteristic = _GATTMeasurementAttribute.characteristic;

            // Get all the child descriptors of a characteristics. Use the cache mode to specify uncached descriptors only
            // and the new Async functions to get the descriptors of unpaired devices as well.
            var result = await _GATTMeasurementCharacteristic.GetDescriptorsAsync(BluetoothCacheMode.Uncached);
            if (result.Status != GattCommunicationStatus.Success)
            {
                return new CharacteristicResult()
                {
                    IsSuccess = false,
                    Message = result.Status.ToString()
                };
            }

            if ((_GATTMeasurementCharacteristic.CharacteristicProperties & GattCharacteristicProperties.Notify) != 0)
            {
                var status = await _GATTMeasurementCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                if (status == GattCommunicationStatus.Success)
                    _GATTMeasurementCharacteristic.ValueChanged += HeartRateValueChanged;

                return new CharacteristicResult()
                {
                    IsSuccess = status == GattCommunicationStatus.Success,
                    Message = status.ToString()
                };
            }
            else
            {
                return new CharacteristicResult()
                {
                    IsSuccess = false,
                    Message = string.Format("{0} characteristic does not support notify", characteristicsName)
                };
            }
        }

        private async Task<bool> GetDeviceServicesAsync()
        {
            // Note: BluetoothLEDevice.GattServices property will return an empty list for unpaired devices. For all uses we recommend using the GetGattServicesAsync method.
            // BT_Code: GetGattServicesAsync returns a list of all the supported services of the device (even if it's not paired to the system).
            // If the services supported by the device are expected to change during BT usage, subscribe to the GattServicesChanged event.
            GattDeviceServicesResult result = await _GATTDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);

            if (result.Status == GattCommunicationStatus.Success)
            {
                _serviceCollection.AddRange(result.Services.Select(a => new BluetoothAttribute(a)));
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Disconnects the current BLE heart rate device.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> DisconnectAsync(bool DeviceHasDisconnected = false)
        {
            await lock_Disconnect.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_GATTDevice == null)
                    return false;
                if (_GATTMeasurementCharacteristic != null)
                {
                    if (!DeviceHasDisconnected)
                    {
                        var result = await _GATTMeasurementCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
                        if (result != GattCommunicationStatus.Success)
                        {
                            return false;
                        }
                    }
                    _GATTMeasurementCharacteristic.ValueChanged -= HeartRateValueChanged;
                }
                if (_GATTMeasurementAttribute != null)
                {
                    try
                    {
                        _GATTMeasurementAttribute?.service?.Dispose();
                    }
                    catch { }
                    _GATTMeasurementAttribute = null;
                }

                if (_GATTAttribute != null)
                {
                    try
                    {
                        _GATTAttribute.service?.Dispose();
                        _GATTAttribute.service = null;
                    }
                    catch { }
                    _GATTAttribute = null;
                }
                foreach (var ser in _serviceCollection)
                {
                    ser.service?.Dispose();
                    ser.service = null;
                }
                _serviceCollection.Clear();
                _GATTDevice?.Dispose();
                _GATTDevice = null;
                GC.Collect();
            }
            finally
            {
                lock_Disconnect.Release();
            }
            return true;
        }
        private async void DeviceConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            var result = new ConnectionStatusChangedEventArgs()
            {
                IsConnected = (sender?.ConnectionStatus == BluetoothConnectionStatus.Connected)
            };
            if (!IsConnected)
            {
                ManualResetEventSlim.Reset();
                try
                {
                    await DisconnectAsync().ConfigureAwait(false);
                }
                catch { }
            }
            OnConnectionStatusChanged(result);
        }

        private void HeartRateValueChanged(GattCharacteristic sender, GattValueChangedEventArgs e)
        {
            CryptographicBuffer.CopyToByteArray(e.CharacteristicValue, out byte[] data);
            byte[] temp = new byte[data.Length + 2];
            Array.Copy(data, 0, temp, 2, data.Length);
            temp[1] = (byte)(data.Length);
            OnDataReceived?.Invoke(temp);
        }

        /// <summary>
        /// Gets a value indicating whether this instance is connected.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is connected; otherwise, <c>false</c>.
        /// </value>
        public bool IsConnected => _GATTDevice?.ConnectionStatus == BluetoothConnectionStatus.Connected;

        private readonly ManualResetEventSlim ManualResetEventSlim = new ManualResetEventSlim(false);
        public void WaitForConnected(CancellationToken cancellationToken)
        {
            if (!ManualResetEventSlim.IsSet)
                ManualResetEventSlim.Wait(cancellationToken);
        }
        public void WaitForConnected()
        {
            if (!ManualResetEventSlim.IsSet)
                ManualResetEventSlim.Wait();
        }
        public string DeviceID => _GATTDevice.DeviceId;
        public async Task<bool> WriteDeviceInfoAsync(string Collection, string CharacteristicName, byte[] Data)
        {
            if (_GATTDevice?.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                var deviceInfoService = _serviceCollection.Find(a => a.Name == Collection);
                var deviceInfocharacteristics = await GetServiceCharacteristicsAsync(deviceInfoService).ConfigureAwait(false);
                return await Utilities.WriteCharacteristicValueAsync(deviceInfocharacteristics, CharacteristicName, Data.AsBuffer()).ConfigureAwait(false);
            }
            else
            {
                return false;
            }
        }
        private async Task<string> ReadDeviceInfoAsync(string Collection, string CharacteristicName)
        {
            if (_GATTDevice?.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                var deviceInfoService = _serviceCollection.Find(a => a.Name == Collection);
                var bluetoothAttributes_GenericAccess = await GetServiceCharacteristicsAsync(deviceInfoService).ConfigureAwait(false);
                return await Utilities.ReadCharacteristicValueAsync(bluetoothAttributes_GenericAccess, CharacteristicName).ConfigureAwait(false);
            }
            else
            {
                return null;
            }
        }
        public async Task<string> GetDeviceName()
        {
            var temp = await ReadDeviceInfoAsync("GenericAccess", "DeviceName").ConfigureAwait(false);
            return temp;
        }

        public async void Dispose()
        {
            await DisconnectAsync(true).ConfigureAwait(false);
        }
    }
}