using System;
using System.Drawing;
using System.Windows.Forms;
using EnvDTE;
using EnvDTE80;
using Extensibility;
using Microsoft.SqlServer.Management.UI.VSIntegration;
using Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer;
using Microsoft.VisualStudio.CommandBars;
using stdole;

namespace InternalsViewer.SSMSAddIn
{
    /// <summary>The object for implementing an Add-in.</summary>
    /// <seealso class='IDTExtensibility2' />
    public class Connect : IDTExtensibility2, IDTCommandTarget
    {
        private QueryEditorExtender queryEditorExtender;
        private ObjectExplorerExtender objectExplorerExtender;
        private WindowManager windowManager;
        private DTE2 applicationObject;
        private EnvDTE.AddIn addInInstance;
        private CommandBarButton openViewerButton;

        public Connect()
        {
        }

        public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
        {
            addInInstance = (AddIn)addInInst;
            applicationObject = (DTE2)addInInstance.DTE;

            switch (connectMode)
            {
                case ext_ConnectMode.ext_cm_Startup:

                    Commands2 commands = (Commands2)applicationObject.Commands;

                    CommandBar menuBarCommandBar = ((CommandBars)applicationObject.CommandBars)["MenuBar"];
                    CommandBar standardCommandBar = ((CommandBars)applicationObject.CommandBars)["Standard"];

                    this.openViewerButton = AddCommandBarButton(commands, 
                                                                standardCommandBar, 
                                                                "OpenInternalsViewer", 
                                                                "Open Internals Viewer", 
                                                                Properties.Resources.allocationMapIcon, 
                                                                Properties.Resources.allocationMapIconMask);

                    CommandBarPopup commandBarPopup = (CommandBarPopup)menuBarCommandBar.Controls.Add(MsoControlType.msoControlPopup, 
                                                                                                      System.Type.Missing, 
                                                                                                      System.Type.Missing, 
                                                                                                      8, 
                                                                                                      Properties.Resources.AppWindow);
                    commandBarPopup.Caption = "Internals Viewer";

                    AddCommandBarPopup(commands, 
                                       commandBarPopup, 
                                       "AllocationMap", 
                                       "Allocation Map", 
                                       "Show the Allocation Map", 
                                       Properties.Resources.allocationMapIcon, 
                                       Properties.Resources.allocationMapIconMask);

                    AddCommandBarPopup(commands, 
                                       commandBarPopup, 
                                       "TransactionLog", 
                                       "Display Transaction Log", 
                                       "Include the Transaction Log with query results", 
                                       Properties.Resources.TransactionLogIcon, 
                                       Properties.Resources.allocationMapIconMask);

                    IObjectExplorerEventProvider provider = ServiceCache.GetObjectExplorer().GetService(typeof(IObjectExplorerEventProvider)) as IObjectExplorerEventProvider;

                    provider.NodesRefreshed += new NodesChangedEventHandler(Provider_NodesRefreshed);
                    provider.NodesAdded += new NodesChangedEventHandler(Provider_NodesRefreshed);
                    provider.BufferedNodesAdded += new NodesChangedEventHandler(Provider_NodesRefreshed);

                    this.windowManager = new WindowManager(applicationObject, addInInstance);
                    this.queryEditorExtender = new QueryEditorExtender(applicationObject, this.windowManager);

                    break;
            }
        }

        /// <summary>
        /// Adds a command bar button.
        /// </summary>
        /// <param name="commands">The commands object.</param>
        /// <param name="commandBar">The command bar.</param>
        /// <param name="commandName">Name of the command.</param>
        /// <param name="description">The description.</param>
        /// <param name="picture">The picture.</param>
        /// <param name="mask">The mask.</param>
        /// <returns></returns>
        private CommandBarButton AddCommandBarButton(Commands2 commands, CommandBar commandBar, string commandName, string description, Bitmap picture, Bitmap mask)
        {
            Command command = null;
            object[] contextGUIDS = null;

            try
            {
                command = commands.Item(addInInstance.ProgID + "." + commandName, 0);
            }
            catch
            {
            }

            if (command == null)
            {
                vsCommandStyle commandStyle = vsCommandStyle.vsCommandStylePict;

                command = commands.AddNamedCommand2(addInInstance,
                                                commandName,
                                                string.Empty,
                                                description,
                                                true,
                                                null,
                                                ref contextGUIDS,
                                                (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled,
                                                (int)commandStyle,
                                                vsCommandControlType.vsCommandControlTypeButton);
            }

            CommandBarButton control = (CommandBarButton)command.AddControl(commandBar, commandBar.Controls.Count + 1);
            control.BeginGroup = true;

            if (picture != null)
            {
                control.Picture = (StdPicture)ImageConverter.GetIPictureDispFromImage(picture);
                control.Mask = (StdPicture)ImageConverter.GetIPictureDispFromImage(mask);
            }

            return control;
        }

        /// <summary>
        /// Adds the command bar popup (menu item).
        /// </summary>
        /// <param name="commands">The commands object.</param>
        /// <param name="commandBar">The command bar.</param>
        /// <param name="name">The name.</param>
        /// <param name="caption">The caption.</param>
        /// <param name="description">The description.</param>
        /// <param name="picture">The picture.</param>
        /// <param name="mask">The mask.</param>
        private void AddCommandBarPopup(Commands2 commands, CommandBarPopup commandBar, string name, string caption, string description, Bitmap picture, Bitmap mask)
        {
            Command command = null;
            object[] contextGUIDS = null;

            try
            {
                command = commands.Item(addInInstance.ProgID + "." + name, 0);
            }
            catch
            {
            }

            if (command == null)
            {
                vsCommandStyle commandStyle = (picture == null) ? vsCommandStyle.vsCommandStyleText : vsCommandStyle.vsCommandStylePictAndText;

                command = commands.AddNamedCommand2(addInInstance,
                                                name,
                                                caption,
                                                description,
                                                true,
                                                null,
                                                ref contextGUIDS,
                                                (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled,
                                                (int)commandStyle,
                                                vsCommandControlType.vsCommandControlTypeButton);
            }

            CommandBarButton control = (CommandBarButton)command.AddControl(commandBar.CommandBar, 1);
            control.Caption = caption;

            if (picture != null)
            {
                control.Picture = (StdPicture)ImageConverter.GetIPictureDispFromImage(picture);
                control.Mask = (StdPicture)ImageConverter.GetIPictureDispFromImage(mask);
            }
        }

        /// <summary>
        /// Handles the NodesRefreshed event of the IObjectExplorerEventProvider object.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer.NodesChangedEventArgs"/> instance containing the event data.</param>
        private void Provider_NodesRefreshed(object sender, NodesChangedEventArgs args)
        {
            Control objectExplorer = (sender as Control);

            if (objectExplorer.InvokeRequired)
            {
                objectExplorer.Invoke(new NodesChangedEventHandler(Provider_NodesRefreshed), new object[] { sender, args });

                return;
            }

            if (objectExplorerExtender == null)
            {
                objectExplorerExtender = new ObjectExplorerExtender(this.windowManager);
            }
        }

        public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
        {
            this.openViewerButton.Delete(null);
        }

        public void OnAddInsUpdate(ref Array custom)
        {
        }

        public void OnStartupComplete(ref Array custom)
        {
        }

        public void OnBeginShutdown(ref Array custom)
        {
        }

        /// <summary>Implements the QueryStatus method of the IDTCommandTarget interface. This is called when the command's availability is updated</summary>
        /// <param term='commandName'>The name of the command to determine state for.</param>
        /// <param term='neededText'>Text that is needed for the command.</param>
        /// <param term='status'>The state of the command in the user interface.</param>
        /// <param term='commandText'>Text requested by the neededText parameter.</param>
        /// <seealso class='Exec' />
        public void QueryStatus(string commandName, vsCommandStatusTextWanted neededText, ref vsCommandStatus status, ref object commandText)
        {
            if (neededText == vsCommandStatusTextWanted.vsCommandStatusTextWantedNone)
            {
                switch (commandName)
                {
                    case "InternalsViewer.SSMSAddIn.Connect.TransactionLog":

                        if (this.queryEditorExtender.DisplayTransactionLog)
                        {
                            status = (vsCommandStatus)vsCommandStatus.vsCommandStatusEnabled | vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusLatched;
                        }
                        else
                        {
                            status = (vsCommandStatus)vsCommandStatus.vsCommandStatusEnabled | vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                        }

                        break;

                    case "InternalsViewer.SSMSAddIn.Connect.AllocationMap":

                        status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                        break;

                    case "InternalsViewer.SSMSAddIn.Connect.OpenInternalsViewer":

                        status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | vsCommandStatus.vsCommandStatusEnabled;
                        break;
                }
            }
        }

        /// <summary>Implements the Exec method of the IDTCommandTarget interface. This is called when the command is invoked.</summary>
        /// <param term='commandName'>The name of the command to execute.</param>
        /// <param term='executeOption'>Describes how the command should be run.</param>
        /// <param term='varIn'>Parameters passed from the caller to the command handler.</param>
        /// <param term='varOut'>Parameters passed from the command handler to the caller.</param>
        /// <param term='handled'>Informs the caller if the command was handled or not.</param>
        /// <seealso class='Exec' />
        public void Exec(string commandName, vsCommandExecOption executeOption, ref object varIn, ref object varOut, ref bool handled)
        {
            handled = false;
            if (executeOption == vsCommandExecOption.vsCommandExecOptionDoDefault)
            {
                switch (commandName)
                {
                    case "InternalsViewer.SSMSAddIn.Connect.TransactionLog":

                        this.queryEditorExtender.DisplayTransactionLog = !queryEditorExtender.DisplayTransactionLog;
                        handled = true;
                        return;

                    case "InternalsViewer.SSMSAddIn.Connect.AllocationMap":
                    case "InternalsViewer.SSMSAddIn.Connect.OpenInternalsViewer":

                        AllocationContainer allocations = windowManager.CreateAllocationWindow();

                        allocations.WindowManager = this.windowManager;
                        
                        allocations.Connect();

                        handled = true;
                        return;
                }
            }
        }
    }
}