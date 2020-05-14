using System.Reflection;
using Nancy;
using Catspaw.Properties;
using System.Globalization;

namespace Catspaw.Api
{
    /// <summary>Define the catspaw api</summary>
    public class CatspawApi : NancyModule
    {
        /// <summary>
        /// Create the catspaw apî with default version number
        /// </summary>
        public CatspawApi(): base("/api/" + Settings.Default.ApiVersion)
        {
            Get("/catspaw_version", args => Assembly.GetExecutingAssembly().FullName);
        }
    }
}
