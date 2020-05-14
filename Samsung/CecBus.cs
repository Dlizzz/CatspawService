using System;
using CecSharp;
using Catspaw.Properties;

namespace Catspaw.Samsung
{
    /// <summary>
    /// Define the Cec bus from libcec
    /// </summary>
    public class CecBus : CecCallbackMethods
    {
        private readonly int timeout;
        private const int controllerId = 0;

        /// <summary>
        /// Initialize the Cec bus without callback methods (no need) 
        /// </summary>
        /// <param name="timeout">Timeout for the connections to the bus. Must be positive</param>
        /// <exception cref="CecException">Unable to initialize Cec Bus or can't find controller</exception>
        /// <exception cref="ArgumentOutOfRangeException">timeout not strictly positive</exception>
        public CecBus(int timeout)
        {
            if (timeout <= 0) throw new ArgumentOutOfRangeException(nameof(timeout));

            this.timeout = timeout;

            Configuration = new LibCECConfiguration();
            Configuration.DeviceTypes.Types[0] = CecDeviceType.PlaybackDevice;
            Configuration.DeviceName = Resources.StrCecDeviceName;
            Configuration.ClientVersion = LibCECConfiguration.CurrentVersion;

            Connection = new LibCecSharp(Configuration) ?? throw new CecException(Resources.ErrorNoCecBus);
            Connection.InitVideoStandalone();

            // Get the controller on the bus
            CecAdapter[] adapters = Connection.FindAdapters(string.Empty);
            if (adapters.Length > 0) Controller = adapters[controllerId];
            else
            {
                Controller = null;
                throw new CecException(Resources.ErrorNoCecController);
            }
        }

        #region Properties
        /// <summary>Get the connection to the bus</summary>
        public LibCecSharp Connection { get; }
        
        /// <summary>Get the configuration of the bus</summary>
        public LibCECConfiguration Configuration { get; }

        /// <summary>Get the controller on the Cec bus</summary>
        public CecAdapter Controller { get; }

        /// <summary>
        /// Get the logical address of the Tv on the cec bus
        /// </summary>
        public static CecLogicalAddress TV => CecLogicalAddress.Tv;

        /// <summary>
        /// Get the current version of LibCec
        /// </summary>
        public string LibCecVersion => Connection.VersionToString(Configuration.ServerVersion);
        #endregion

        #region Power Management
        /// <summary>
        /// Switch power state of the given device to the requested power state
        /// </summary>
        /// <param name="device"></param>
        /// <param name="requestedPowerState"></param>
        /// <returns cref="PowerState">New PowerSate. Power state is unknown if the switch fails</returns>
        /// <exception cref="CecException">Can't connect to CecBus</exception>
        public PowerState SwitchDevicePowerState(CecLogicalAddress device, PowerState requestedPowerState)
        {
            PowerState state = PowerState.PowerUnknown;
            
            try
            {
                Connect();
            }
            catch (CecException) { throw; }

            if (IsDeviceReady(device))
            {
                switch (requestedPowerState)
                {
                    case PowerState.PowerOff:
                        if (Connection.StandbyDevices(device)) state = PowerState.PowerOff;
                        break;
                    case PowerState.PowerOn:
                        if (Connection.PowerOnDevices(device)) state = PowerState.PowerOn;
                        break;
                    default:
                        break;
                }
            }
            Disconnect();

            return state;
        }

        /// <summary>
        /// Get power state of the given devive
        /// </summary>
        /// <param name="device"></param>
        /// <returns>The power state of the device. Power state is unknown if the query fails</returns>
        /// <exception cref="CecException">Can't connect to CecBus</exception>
        public PowerState GetDevicePowerState(CecLogicalAddress device)
        {
            PowerState state = PowerState.PowerUnknown;

            try
            {
                Connect();
            }
            catch (CecException) { throw; }

            if (IsDeviceReady(device))
            {
                CecPowerStatus cecStatus = Connection.GetDevicePowerStatus(device);

                state = cecStatus switch
                {
                    CecPowerStatus.On => PowerState.PowerOn,
                    CecPowerStatus.Standby => PowerState.PowerOff,
                    _ => PowerState.PowerUnknown
                };
            }

            Disconnect();
            return state;
        }
        #endregion

        #region Connection
        // Return True if device is ready to recieve command
        private bool IsDeviceReady(CecLogicalAddress device) =>
            (Connection.IsActiveDevice(device) && Connection.PollDevice(device));

        // Connect to the Cec bus controller. 
        private void Connect()
        {
            // Fails if no controller
            if (Controller == null) throw new CecException(Resources.ErrorNoCecController);

            // Try to connect to the controller
            if (!Connection.Open(Controller.ComPort, timeout))
                throw new CecException(Resources.ErrorNoCecControllerConnection);

            // Register active source of the connection
            Connection.SetActiveSource(CecDeviceType.PlaybackDevice);
        }

        // Close the connection to the Cec bus controller
        private void Disconnect() => Connection.Close();
        #endregion

        #region Dispose support
        // Flag: Has Dispose already been called?
        private bool disposed = false;

        /// <summary>
        /// Close and dispose the Cec bus connection. Override the base class Dispose method
        /// </summary>
        /// <param name="disposing">True to dispose</param>
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                Connection.Close();
                if (Connection != null) Connection.Dispose();
            }
            disposed = true;
            // Call base class implementation.
            base.Dispose(disposing);
        }
        #endregion
    }
}
