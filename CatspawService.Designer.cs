using System.ComponentModel;
using System.Diagnostics;

namespace Catspaw
{
    partial class CatspawService
    {
        /// <summary> 
        /// Variable nécessaire au concepteur.
        /// </summary>
        private IContainer components = null;

        /// <summary>
        /// Nettoyage des ressources utilisées.
        /// </summary>
        /// <param name="disposing">true si les ressources managées doivent être supprimées ; sinon, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                ServiceStop();
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Code généré par le Concepteur de composants

        /// <summary> 
        /// Méthode requise pour la prise en charge du concepteur - ne modifiez pas 
        /// le contenu de cette méthode avec l'éditeur de code.
        /// </summary>
        private void InitializeComponent()
        {
            this.eventLog = new System.Diagnostics.EventLog();
            ((System.ComponentModel.ISupportInitialize)(this.eventLog)).BeginInit();
            // 
            // CatspawService
            // 
            this.AutoLog = false;
            this.CanHandlePowerEvent = true;
            this.CanShutdown = true;
            this.ServiceName = "CatspawService";
            ((System.ComponentModel.ISupportInitialize)(this.eventLog)).EndInit();

        }

        #endregion

        private EventLog eventLog;
    }
}
