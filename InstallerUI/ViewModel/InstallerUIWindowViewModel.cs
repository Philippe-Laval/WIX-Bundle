using InstallerUI.Bootstrapper;
using InstallerUI.Interfaces;
using Microsoft.Practices.Prism.Commands;
using Microsoft.Practices.Prism.Mvvm;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
using Newtonsoft.Json;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Input;

/*
WiX Burn has three main phases which happen asynchronously:

1. Detect - It should be the first process that should run as soon as the Bootstrapper starts because it needs to decide what button should be active from the UI: Install or Uninstall.
During this phase, the engine will raise the OnDetect events to tell the Bootstrapper Application what it finds;

2. Plan - This is the phase where the Burn engine specifies the desired operation by calling Engine.Plan(). This is done, usually, right before Apply phase, after the user clicks on UI buttons.
The OnPlan events are raised in this phase;

3. Apply - This is the phase where the Burn engine is installing or uninstalling the packages in the bundle and starts when the Bootstrapper application calls Engine.Apply(). Most of the events are raised in this phase and are related to the progress, error reporting or to allow the bootstrapper application to handle certain things. Apply has two sub-phases: Cache and Execute;

There are three more events that are not raised during the previous main phases:
    OnStartup - Raised when the Bootstrapper first start.
    OnShutdown - Raised when the Bootstrapper is quitting.
    OnSystemShutdown - raised when the WM_QUERYENDSESSION window message is received.
*/


namespace InstallerUI.ViewModel
{
    [Export]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class InstallerUIWindowViewModel : BindableBase
    {
        private readonly int port = 3307;
        private BootstrapperApplication bootstrapper;
        private Engine engine;
        private readonly BootstrapperBundleData bootstrapperBundleData;

        [Import]
        private IInteractionService interactionService = null;

        [Import(typeof(IMySQLService))]
        private IMySQLService MySQLService { get; set; }

        #region Properties for data binding
        private DelegateCommand InstallCommandValue;
        public ICommand InstallCommand { get { return InstallCommandValue; } }

        private DelegateCommand UninstallCommandValue;
        public ICommand UninstallCommand { get { return UninstallCommandValue; } }

        private readonly DelegateCommand CancelCommandValue;
        public ICommand CancelCommand { get { return CancelCommandValue; } }

        private InstallationStatus StatusValue;
        public InstallationStatus Status
        {
            get => StatusValue;
            set
            {
                SetProperty(ref StatusValue, value);
                InstallCommandValue.RaiseCanExecuteChanged();
                UninstallCommandValue.RaiseCanExecuteChanged();
            }
        }

        private bool DowngradeValue;
        public bool Downgrade
        {
            get => DowngradeValue;
            set => SetProperty(ref DowngradeValue, value);
        }

        private int LocalProgressValue;
        public int LocalProgress
        {
            get => LocalProgressValue;
            set => SetProperty(ref LocalProgressValue, value);
        }

        private int GlobalProgressValue;
        public int GlobalProgress
        {
            get => GlobalProgressValue;
            set => SetProperty(ref GlobalProgressValue, value);
        }

        private string ProgressValue;
        public string Progress
        {
            get => ProgressValue;
            set => SetProperty(ref ProgressValue, value);
        }

        private string CurrentPackageValue;
        public string CurrentPackage
        {
            get => CurrentPackageValue;
            set => SetProperty(ref CurrentPackageValue, value);
        }

        private bool InstallingValue;
        public bool Installing
        {
            get => InstallingValue;
            set
            {
                SetProperty(ref InstallingValue, value);
                InstallCommandValue.RaiseCanExecuteChanged();
                UninstallCommandValue.RaiseCanExecuteChanged();
            }
        }

        private bool isCancelledValue;
        public bool IsCancelled
        {
            get => isCancelledValue;
            set
            {
                SetProperty(ref isCancelledValue, value);
            }
        }

        private string CurrentActionValue;
        public string CurrentAction
        {
            get => CurrentActionValue;
            set
            {
                SetProperty(ref CurrentActionValue, value);
            }
        }
        #endregion

        /// <summary>
        /// In the class constructor, we set up the event handlers and setup commands. 
        /// We extend the implementation for the BootstrapperApplication in the namespace Microsoft.Tools.WindowsInstallerXml.Bootstrapper
        /// and we import parts of the MEF extension classes to extend functionality using composition. 
        /// The InstallerUIWindowsViewModel constructor will receive BootstrapperApplication and Engine as parameters.
        /// </summary>
        /// <param name="bootstrapper">BootstrapperApplication</param>
        /// <param name="engine">Engine</param>
        [ImportingConstructor]
        public InstallerUIWindowViewModel(BootstrapperApplication bootstrapper, Engine engine)
        {
            // Get the content of the "BootstrapperApplicationData.xml" file contianing the different Package (msi or exe installers)
            bootstrapperBundleData = new BootstrapperBundleData();
            this.bootstrapper = bootstrapper;
            this.engine = engine;

            #region Setup commands

            // There are three setup commands defined: Install, Uninstall and Cancel.
            // These commands will enable or disable the UI controls that are bound to them.
            // Engine.Plan will launch an action, Install or Uninstall.
            InstallCommandValue = new DelegateCommand(
                 () => engine.Plan(LaunchAction.Install),
                 () => !Installing && Status == InstallationStatus.DetectedAbsent);
            UninstallCommandValue = new DelegateCommand(
                () => engine.Plan(LaunchAction.Uninstall),
                () => !Installing && Status == InstallationStatus.DetectedPresent);
            CancelCommandValue = new DelegateCommand(
                () => IsCancelled = true);

            #endregion

            #region Event Handlers 

            // DetectBegin - Start checking if the Bootstrapper package is installed / uninstalled.
            // Here we set the Status variable that can be used to change the UI according to current action we are in.
            bootstrapper.DetectBegin += (_, ea) =>
            {
                LogEvent("DetectBegin", ea);
                CurrentAction = ea.Installed ? "Preparing for software uninstall" : "Preparing for software install";
                interactionService.RunOnUIThread(
                    () => Status = ea.Installed ? InstallationStatus.DetectedPresent : InstallationStatus.DetectedAbsent);
            };

            bootstrapper.DetectRelatedBundle += (_, ea) =>
            {
                LogEvent("DetectRelatedBundle", ea);


                interactionService.RunOnUIThread(() => Downgrade |= ea.Operation == RelatedOperation.Downgrade);
            };

            bootstrapper.DetectComplete += (s, ea) =>
            {
                LogEvent("DetectComplete");
                DetectComplete(s, ea);
            };

            bootstrapper.PlanComplete += (_, ea) =>
            {
                LogEvent("PlanComplete", ea);

                if (ea.Status >= 0)
                {
                    engine.Apply(interactionService.GetMainWindowHandle());
                }
            };

            bootstrapper.ApplyBegin += (_, ea) =>
            {
                LogEvent("ApplyBegin");

                interactionService.RunOnUIThread(() => Installing = true);
            };

            // ExecutePackageBegin - Triggered when individual package installation/uninstallation begins.
            // Here we display the current installation package name using a property that binds to a WPF UI label element.
            bootstrapper.ExecutePackageBegin += (_, ea) =>
            {
                LogEvent("ExecutePackageBegin", ea);
                CurrentAction = this.Status == InstallationStatus.DetectedAbsent ? "We are installing software" : "We are uninstalling software";

                interactionService.RunOnUIThread(() =>
                CurrentPackage = String.Format("Current package: {0}",
                bootstrapperBundleData.Data.Packages.Where(p => p.Id == ea.PackageId).FirstOrDefault().DisplayName));
            };

            // ExecutePackageComplete - Triggered when individual package action begins.
            // Here we check, for example, if the MySQL Community Installer finished running, 
            // we can start a server installation and configuration.
            bootstrapper.ExecutePackageComplete += (_, ea) =>
            {
                if (Status == InstallationStatus.DetectedAbsent && IsCancelled != true)
                {
                    if (ea.PackageId == "MySQL")
                    {
                        CurrentAction = "Installing & Configuring MySQL Server";
                        MySQLService.InitServer(port);
                    }
                }

                LogEvent("ExecutePackageComplete", ea);

                interactionService.RunOnUIThread(() => CurrentPackage = string.Empty);
            };

            // ExecuteProgress - Gets triggered when progress changes.
            // In this area, we also check if "Cancel" button is pressed (it will do a roll-back in case this button is pressed).
            bootstrapper.ExecuteProgress += (_, ea) =>
            {
                LogEvent("ExecuteProgress", ea);
                if (IsCancelled == true)
                {
                    ea.Result = Result.Abort;
                }
                
                Progress = String.Format("Progress: {0}{1}", ea.OverallPercentage.ToString(), "%");

                interactionService.RunOnUIThread(() =>
                {
                    LocalProgress = ea.ProgressPercentage;
                    GlobalProgress = ea.OverallPercentage;
                });
            };

            // ApplyComplete - it is triggered when an action is completed.
            bootstrapper.ApplyComplete += (_, ea) =>
            {
                LogEvent("ApplyComplete", ea);
                interactionService.CloseUIAndExit();
            };
            #endregion
        }

        private void DetectComplete(object sender, DetectCompleteEventArgs e)
        {
            if (LaunchAction.Uninstall == bootstrapper.Command.Action)
            {
                engine.Log(LogLevel.Verbose, "Invoking automatic plan for uninstall");
                engine.Plan(LaunchAction.Uninstall);
            }
        }

        private void LogEvent(string eventName, EventArgs arguments = null)
        {
            engine.Log(
                LogLevel.Verbose,
                arguments == null ? string.Format("EVENT: {0}", eventName)
                                    :
                                    string.Format("EVENT: {0} ({1})",
                                                  eventName,
                                                  JsonConvert.SerializeObject(arguments))
            );
        }
    }
}
