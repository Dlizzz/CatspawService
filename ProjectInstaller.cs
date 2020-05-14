using System.ComponentModel;
using System.Configuration.Install;

namespace Catspaw
{
    /// <summary>
    /// Project installer class
    /// </summary>
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        /// <summary>
        /// Initialize project installer, including service installer
        /// </summary>
        public ProjectInstaller()
        {
            InitializeComponent();
        }
    }
}
