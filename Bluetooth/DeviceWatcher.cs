using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;

namespace Bluetooth.GATT
{
    public class BLEDeviceWatcher : IDisposable
    {
        // Additional properties we would like about the device.
        // Property strings are documented here https://msdn.microsoft.com/en-us/library/windows/desktop/ff521659(v=vs.85).aspx
        private readonly string[] additionalProperties =
            {
                "System.Devices.Aep.CanPair",
                "System.Devices.Aep.IsConnected",
                "System.Devices.Aep.IsPresent",
                "System.Devices.Aep.IsPaired"
            };

        private DeviceWatcher _deviceWatcher;
        private BluetoothLEAdvertisementWatcher _bluetoothLEAdvertisementWatcher;
        private readonly HashSet<string> _filters;
        private readonly Dictionary<ulong, int> SignalStrengthList = new Dictionary<ulong, int>();
        /// <summary>
        /// Interval of signal strength scan(ms)
        /// </summary>
        public uint SignalStrengthInterval { get; set; } = 500;

        public event EventHandler<Events.DeviceAddedEventArgs> DeviceAdded;
        protected virtual void OnDeviceAdded(Events.DeviceAddedEventArgs e)
        {
            DeviceAdded?.Invoke(this, e);
        }

        public event EventHandler<Events.DeviceUpdatedEventArgs> DeviceUpdated;
        protected virtual void OnDeviceUpdated(Events.DeviceUpdatedEventArgs e)
        {
            DeviceUpdated?.Invoke(this, e);
        }

        public event EventHandler<Events.DeviceRemovedEventArgs> DeviceRemoved;
        protected virtual void OnDeviceRemoved(Events.DeviceRemovedEventArgs e)
        {
            DeviceRemoved?.Invoke(this, e);
        }

        public event EventHandler<object> DeviceEnumerationCompleted;
        protected virtual void OnDeviceEnumerationCompleted(object obj)
        {
            DeviceEnumerationCompleted?.Invoke(this, obj);
        }

        public event EventHandler<object> DeviceEnumerationStopped;
        protected virtual void OnDeviceEnumerationStopped(object obj)
        {
            DeviceEnumerationStopped?.Invoke(this, obj);
        }

        public event EventHandler<Dictionary<ulong, int>> SignalStrengthUpdated;
        protected virtual void OnSignalStrengthUpdated(Dictionary<ulong, int> e)
        {
            SignalStrengthUpdated?.Invoke(this, e);
        }

        public BLEDeviceWatcher(Schema.DeviceSelector deviceSelector, uint SignalStrengthInterval = 1000)
        {
            _deviceWatcher = DeviceInformation.CreateWatcher(
                        GetSelector(deviceSelector),
                        additionalProperties,
                        DeviceInformationKind.AssociationEndpoint);

            _bluetoothLEAdvertisementWatcher = new BluetoothLEAdvertisementWatcher();
            this.SignalStrengthInterval = SignalStrengthInterval;
            _bluetoothLEAdvertisementWatcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(SignalStrengthInterval);
            _bluetoothLEAdvertisementWatcher.SignalStrengthFilter.InRangeThresholdInDBm = -80;
            _bluetoothLEAdvertisementWatcher.SignalStrengthFilter.OutOfRangeThresholdInDBm = -100;
            _bluetoothLEAdvertisementWatcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(2000);
            _bluetoothLEAdvertisementWatcher.Received += Received;

            SendDictionary();
            _deviceWatcher.Added += Added;
            _deviceWatcher.Updated += Updated;
            _deviceWatcher.Removed += Removed;
            _deviceWatcher.EnumerationCompleted += EnumerationCompleted;
            _deviceWatcher.Stopped += Stopped;
        }
        public BLEDeviceWatcher(Schema.DeviceSelector deviceSelector, List<string> filters, uint SignalStrengthInterval = 1000) : this(deviceSelector, SignalStrengthInterval)
        {
            _filters = filters.ToHashSet();
        }

        private void Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (SignalStrengthList.Count >= 1000)
                SignalStrengthList.Clear();
            if (args.RawSignalStrengthInDBm == -127 && SignalStrengthList.ContainsKey(args.BluetoothAddress))
            {
                SignalStrengthList.Remove(args.BluetoothAddress);
            }
            else if (SignalStrengthList.ContainsKey(args.BluetoothAddress))
            {
                SignalStrengthList[args.BluetoothAddress] = args.RawSignalStrengthInDBm;
            }
            else
            {
                SignalStrengthList.Add(args.BluetoothAddress, args.RawSignalStrengthInDBm);
            }
        }

        private async void SendDictionary()
        {
            while (true)
            {
                OnSignalStrengthUpdated(SignalStrengthList);
                await Task.Delay((int)SignalStrengthInterval).ConfigureAwait(false);
            }
        }

        private string GetSelector(Schema.DeviceSelector deviceSelector)
        {
            switch (deviceSelector)
            {
                case Schema.DeviceSelector.BluetoothLePairedOnly:
                    return BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
                case Schema.DeviceSelector.BluetoothLeUnpairedOnly:
                    return BluetoothLEDevice.GetDeviceSelectorFromPairingState(false);
                default:
                    return "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";
            }
        }

        private void Stopped(DeviceWatcher watcher, object obj)
        {
            OnDeviceEnumerationStopped(obj);
        }

        private void EnumerationCompleted(DeviceWatcher sender, object obj)
        {
            // Protect against race condition if the task runs after the app stopped the deviceWatcher.
            if (sender == _deviceWatcher)
                OnDeviceEnumerationCompleted(obj);
        }

        private bool IsDeviceCompatible(DeviceInformation DeviceInformation)
        {
            var compatibleDevice = true;
            try
            {
                //if filters were passed, check if the device name contains one of the names in the list
                if (_filters != null)
                {
                    compatibleDevice = _filters.Contains(DeviceInformation.Name);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                compatibleDevice = false;
            }

            return compatibleDevice;
        }

        private void Added(DeviceWatcher sender, DeviceInformation deviceInformation)
        {
            if (IsDeviceCompatible(deviceInformation))
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == _deviceWatcher)
                {
                    int SignalStrength = int.MinValue;
                    ulong mac = (ulong)Convert.ToInt64(deviceInformation.Id.Substring(deviceInformation.Id.Length - 17, 17).Replace(":", ""), 16);
                    if (SignalStrengthList.ContainsKey(mac))
                        SignalStrength = SignalStrengthList[mac];
                    var args = new Events.DeviceAddedEventArgs()
                    {
                        Device = new Schema.WatcherDevice()
                        {
                            Id = deviceInformation.Id,
                            Address = mac,
                            IsDefault = deviceInformation.IsDefault,
                            IsEnabled = deviceInformation.IsEnabled,
                            Name = deviceInformation.Name,
                            IsPaired = deviceInformation.Pairing.IsPaired,
                            IsConnected = deviceInformation.Properties.TryGetValue("System.Devices.Aep.IsConnected", out object value) && (bool)value,
                            Kind = deviceInformation.Kind.ToString(),
                            Properties = deviceInformation.Properties.ToDictionary(pair => pair.Key, pair => pair.Value),
                            SignalStrength = SignalStrength
                        }
                    };
                    OnDeviceAdded(args);
                }
            }
        }

        private void Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInformationUpdate)
        {
            //if (await IsDeviceCompatible(deviceInformationUpdate))
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == _deviceWatcher)
                {
                    var args = new Events.DeviceUpdatedEventArgs()
                    {
                        Device = new Schema.WatcherDevice()
                        {
                            Id = deviceInformationUpdate.Id,
                            Kind = deviceInformationUpdate.Kind.ToString(),
                            IsConnected = deviceInformationUpdate.Properties.TryGetValue("System.Devices.Aep.IsConnected", out object value) && (bool)value,
                            Properties = deviceInformationUpdate.Properties.ToDictionary(pair => pair.Key, pair => pair.Value)
                        }
                    };
                    OnDeviceUpdated(args);
                }
            }
        }

        private void Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInformationUpdate)
        {
            //if (await IsDeviceCompatible(deviceInformationUpdate.))
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == _deviceWatcher)
                {
                    var args = new Events.DeviceRemovedEventArgs()
                    {
                        Device = new Schema.WatcherDevice()
                        {
                            Id = deviceInformationUpdate.Id,
                            Kind = deviceInformationUpdate.Kind.ToString(),
                            IsConnected = deviceInformationUpdate.Properties.TryGetValue("System.Devices.Aep.IsConnected", out object value) && (bool)value,
                            Properties = deviceInformationUpdate.Properties.ToDictionary(pair => pair.Key, pair => pair.Value)
                        }
                    };

                    OnDeviceRemoved(args);
                }
            }
        }

        public bool Running { get; private set; }

        public void Start()
        {
            _deviceWatcher.Start();
            _bluetoothLEAdvertisementWatcher.Start();
            Running = true;
        }

        public void Stop()
        {
            if (_deviceWatcher.Status != DeviceWatcherStatus.Stopped && _deviceWatcher.Status != DeviceWatcherStatus.Stopping)
                _deviceWatcher.Stop();
            if (_bluetoothLEAdvertisementWatcher.Status != BluetoothLEAdvertisementWatcherStatus.Stopped && _bluetoothLEAdvertisementWatcher.Status != BluetoothLEAdvertisementWatcherStatus.Stopping)
                _bluetoothLEAdvertisementWatcher.Stop();
            Running = false;
        }

        public void Dispose()
        {
            Stop();
            _bluetoothLEAdvertisementWatcher = null;
            _deviceWatcher = null;
        }
    }
}

