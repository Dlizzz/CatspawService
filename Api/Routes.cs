using System.Reflection;
using Nancy;
using Catspaw.Properties;
using System.Globalization;

namespace Catspaw.Api
{
    /// <summary>Define the catspaw api</summary>
    public class Routes : NancyModule
    {
        /// <summary>
        /// Create the catspaw apî with default version number
        /// </summary>
        public Routes() : base("/api/" + Resources.StrApiVersion)
        {
            Get("/version", _ => Assembly.GetExecutingAssembly().FullName);
            Get("/poweroff", _ => (NativeMethods.SetSuspendState(false, false, false) != 0) ? HttpStatusCode.OK : HttpStatusCode.InternalServerError);
        }
    }
}
