using System;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace Catspaw
{
    #region Global constants and enum
    /// <summary>
    /// Define the power states for the system components 
    /// </summary>
    public enum PowerState : int
    {
        /// <summary>The component is switched on</summary>
        PowerOn,
        /// <summary>The component is switched off</summary>
        PowerOff,
        /// <summary>The power state for the component is unknown</summary>
        PowerUnknown
    }
    #endregion

    #region Native methods
    // Commentaire XML manquant pour le type ou le membre visible publiquement
    // Les identificateurs ne doivent pas contenir de traits de soulignement
    // Remplacer Equals et l'opérateur égal à dans les types valeur
    // Ne pas déclarer de champs d'instances visibles
#pragma warning disable CS1591, CA1707, CA1815, CA1051
    public enum ServiceState
    {
        SERVICE_STOPPED = 0x00000001,
        SERVICE_START_PENDING = 0x00000002,
        SERVICE_STOP_PENDING = 0x00000003,
        SERVICE_RUNNING = 0x00000004,
        SERVICE_CONTINUE_PENDING = 0x00000005,
        SERVICE_PAUSE_PENDING = 0x00000006,
        SERVICE_PAUSED = 0x00000007,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ServiceStatus
    {
        public int dwServiceType;
        public ServiceState dwCurrentState;
        public int dwControlsAccepted;
        public int dwWin32ExitCode;
        public int dwServiceSpecificExitCode;
        public int dwCheckPoint;
        public int dwWaitHint;
    };
#pragma warning restore CS1591, CA1707, CA1815, CA1051

    internal static class NativeMethods
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool SetServiceStatus(IntPtr handle, ref ServiceStatus serviceStatus);

        /// <summary>
        /// Suspends the system by shutting power down. Depending on the Hibernate parameter, the system either enters a suspend (sleep) state or hibernation (S4).
        /// </summary>
        /// <param name="hibernate">If this parameter is TRUE, the system hibernates. If the parameter is FALSE, the system is suspended.</param>
        /// <param name="forceCritical">Windows Server 2003, Windows XP, and Windows 2000:  If this parameter is TRUE,
        /// the system suspends operation immediately; if it is FALSE, the system broadcasts a PBT_APMQUERYSUSPEND event to each
        /// application to request permission to suspend operation.</param>
        /// <param name="disableWakeEvent">If this parameter is TRUE, the system disables all wake events. If the parameter is FALSE, any system wake events remain enabled.</param>
        /// <returns>If the function succeeds, the return value is true.</returns>
        /// <remarks>See http://msdn.microsoft.com/en-us/library/aa373201(VS.85).aspx</remarks>
        [DllImport("Powrprof.dll", SetLastError = true)]
        internal static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
    }
    #endregion

    static class Program
    {
        /// <summary>
        /// Main entry point of the application
        /// </summary>
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new CatspawService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
