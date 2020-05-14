using System;
using System.Diagnostics;
using System.Globalization;
using System.ServiceProcess;
using System.Threading;
using Nancy.Hosting.Self;
using Catspaw.Properties;
using Catspaw.Pioneer;
using Catspaw.Samsung;

namespace Catspaw
{
    /// <summary>
    /// CatspawService base class with event handlers
    /// </summary>
    public partial class CatspawService : ServiceBase
    {
        // HTPC components
        private Avr pioneerAvr;
        private Tv samsungTv;

        // Api web server
        private NancyHost apiServer;
        private Uri apiUri;

        // To make ResumeAutomatic and ResumeSuspend mutually exclusive
        private const int INT_FALSE = 0, INT_TRUE = 1; 
        private int isResumed = INT_FALSE;

        #region Initialization
        /// <summary>
        /// Initialize CatspawService: event log
        /// </summary>
        public CatspawService()
        {
            InitializeComponent();
        }
        #endregion

        #region Start, Stop, Shutdown Events
        /// <summary>
        /// Service starting actions. Initialize the Avr and the Tv. Report to enventlog and 
        /// to ServiceControlManager
        /// </summary>
        /// <param name="args">Command line arguments</param>
        protected override void OnStart(string[] args)
        {
            // Update the service state to Start Pending.
            ServiceStatus serviceStatus = new ServiceStatus
            {
                dwCurrentState = ServiceState.SERVICE_START_PENDING,
                // Starting time in milliseconds
                dwWaitHint = 20000
            };
            NativeMethods.SetServiceStatus(ServiceHandle, ref serviceStatus);

            // Report service is starting
            eventLog.WriteEntry(Resources.EventStrStarting, EventLogEntryType.Information,
                int.Parse(Resources.EventIdStarting, CultureInfo.InvariantCulture));

            // Initialize Pioneer AVR
            try
            {
                pioneerAvr = new Avr(Settings.Default.AvrHostname, Settings.Default.AvrPort);
            }
            catch (AvrException e)
            {
                pioneerAvr = null;
                eventLog.WriteEntry(Resources.EventIdAddingComponentFailure
                    + $" {pioneerAvr.Hostname}:{pioneerAvr.Port}"
                    + $" ; {e.Message}",
                    EventLogEntryType.Warning,
                    int.Parse(Resources.EventIdAddingComponentFailure, CultureInfo.InvariantCulture));
            }
            if (pioneerAvr != null)
                eventLog.WriteEntry(Resources.EventIdAddingComponentSuccess 
                    + $" {pioneerAvr.Hostname}:{pioneerAvr.Port}",
                    EventLogEntryType.Information, 
                    int.Parse(Resources.EventIdAddingComponentSuccess, CultureInfo.InvariantCulture));

            //Initialize Samsung TV
            try
            {
                samsungTv = new Tv();
            }
            catch (CecException e)
            {
                samsungTv = null;
                eventLog.WriteEntry(Resources.EventIdAddingComponentFailure
                    + $" {samsungTv.Controller.Path}:{samsungTv.Controller.ComPort}"
                    + $" ; {e.Message}",
                    EventLogEntryType.Warning,
                    int.Parse(Resources.EventIdAddingComponentFailure, CultureInfo.InvariantCulture));
            }
            if (samsungTv != null)
                eventLog.WriteEntry(Resources.EventIdAddingComponentSuccess 
                    + $" {samsungTv.Controller.Path}:{samsungTv.Controller.ComPort}",
                    EventLogEntryType.Information,
                    int.Parse(Resources.EventIdAddingComponentSuccess, CultureInfo.InvariantCulture));

            // Initialize Api Server and start the Api server
            var apiServerConfig = new HostConfiguration
            {
                UrlReservations = new UrlReservations() { CreateAutomatically = true }
            };
            // Warning: Uri must terminate with / or server will crash when starting
            apiUri = new Uri(
                "http://localhost:"
                + Settings.Default.ApiServerPort.ToString(CultureInfo.InvariantCulture)
                + "/" + Settings.Default.ApiServerRoot + "/");
            apiServer = new NancyHost(apiServerConfig, apiUri);
            apiServer.Start();

            // Report service is started
            var message = Resources.EventStrStarted + " and listening on " + apiUri.ToString();
            eventLog.WriteEntry(message, EventLogEntryType.Information,
                int.Parse(Resources.EventIdStarting, CultureInfo.InvariantCulture));

            // Update the service state to Running.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            NativeMethods.SetServiceStatus(ServiceHandle, ref serviceStatus);
        }

        /// <summary>
        /// Service stopping actions. Dispose Avr and TV. Report to eventtlog.  
        /// </summary>
        protected override void OnStop()
        {
            // Report service is stopping
            eventLog.WriteEntry(Resources.EventStrStopping, EventLogEntryType.Information,
                int.Parse(Resources.EventIdStopping, CultureInfo.InvariantCulture));

            ServiceStop();

            // Report service is stopped
            eventLog.WriteEntry(Resources.EventStrStopped, EventLogEntryType.Information,
                int.Parse(Resources.EventIdStopped, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Service shutdown actions. Dispose Avr and TV. Report to eventtlog.  
        /// </summary>
        protected override void OnShutdown()
        {
            // Report service is going to shutdown
            eventLog.WriteEntry(Resources.EventStrShutdowning, EventLogEntryType.Information,
                int.Parse(Resources.EventIdShutdowning, CultureInfo.InvariantCulture));

            ServiceStop();

            // Report service is shutdown
            eventLog.WriteEntry(Resources.EventStrShutdown, EventLogEntryType.Information,
                int.Parse(Resources.EventIdShutdown, CultureInfo.InvariantCulture));

            base.OnShutdown();
        }

        private void ServiceStop()
        {
            // Stop the server
            apiServer.Stop();

            // Disposing resources
            pioneerAvr?.Dispose();
            samsungTv?.Dispose();
        }
        #endregion

        #region Power Events
        /// <summary>
        /// Service power events actions.
        /// </summary>
        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            switch (powerStatus)
            {
                case PowerBroadcastStatus.Suspend:
                    // Reset isResumed status
                    isResumed = INT_FALSE;

                    // Report service is going to suspend mode
                    eventLog.WriteEntry(Resources.EventStrSuspend, EventLogEntryType.Information,
                        int.Parse(Resources.EventIdSuspend, CultureInfo.InvariantCulture));

                    // If we have a Tv, try to power it off. Report if it fails.
                    try
                    {
                        samsungTv?.PowerOff();
                    }
                    catch (CecException e)
                    {
                        eventLog.WriteEntry($"Powering off TV set failed: {e.Message}", EventLogEntryType.Error);
                    }
                    // If we have an Avr, try to power it off. Report if it fails.
                    try
                    {
                        pioneerAvr?.PowerOff();
                    }
                    catch (AvrException e)
                    {
                        eventLog.WriteEntry($"Powering off AVR failed: {e.Message}", EventLogEntryType.Error);
                    }
                    break;

                // ResumeSuspend and ResumeAutomatic must be mutually exclusive as they could be executed 
                // concurrently by different threads issued from SCM thread pool
                case PowerBroadcastStatus.ResumeSuspend:
                case PowerBroadcastStatus.ResumeAutomatic:
                    if (Interlocked.CompareExchange(ref isResumed, INT_TRUE, INT_FALSE) == INT_FALSE)
                    {
                        // Report service is waking up
                        eventLog.WriteEntry(Resources.EventStrWakeUp, EventLogEntryType.Information,
                            int.Parse(Resources.EventIdWakeUp, CultureInfo.InvariantCulture));

                        // If we have a Tv, try to power it on. Report if it fails.
                        try
                        {
                            samsungTv?.PowerOn();
                        }
                        catch (CecException e)
                        {
                            eventLog.WriteEntry($"Powering on TV set failed: {e.Message}", EventLogEntryType.Error);
                        }
                        // If we have an Avr, try to power it on. Report if it fails.
                        try
                        {
                            pioneerAvr?.PowerOn();
                        }
                        catch (AvrException e)
                        {
                            eventLog.WriteEntry($"Powering on AVR failed: {e.Message}", EventLogEntryType.Error);
                        }
                    }
                    break;

                default:
                    break;
            }

            // We always accept suspension
            return true;
        }
        #endregion
    }
}
