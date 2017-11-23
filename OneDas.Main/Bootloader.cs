﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading.Tasks;
using OneDas.Common;
using OneDas.Infrastructure;
using OneDas.Main.Core;
using OneDas.Main.Shell;
using OneDas.Main.Web;
using Microsoft.AspNetCore.Hosting;

namespace OneDas.Main
{
    public static class Bootloader
    {
        #region "Events"

        public static event EventHandler<SystemMessageReceivedEventArgs> SystemMessageReceived;

        #endregion

        #region "Fields"

        private static OneDasController _oneDasController;
        private static OneDasConsole _oneDasConsole;
        private static ServiceController _oneDasServiceController;
        private static Task _task_WebHost;
        private static IWebHost _webhost;

        #endregion

        #region "Properties"

        public static bool IsElevated { get; private set; }

        /// <summary>
        /// Gets or sets the last error that occured in the OneDAS server.
        /// </summary>
        /// <returns>Returns last error that occured in the OneDAS server.</returns>
        public static string LastError { get; set; }

        public static OneDasController OneDasController
        {
            get
            {
                return _oneDasController;
            }
        }

        public static HomeController HomeController { get; set; }

        #endregion

        #region "Methods"

        public static void Main(string[] args)
        {
            string configurationDirectoryPath;

            // configuration
            configurationDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OneDAS");

            Directory.CreateDirectory(configurationDirectoryPath);
            ConfigurationManager<OneDasSettings>.Initialize(configurationDirectoryPath, "oneDassettings.json");

            // base directory path
            if (!Directory.Exists(ConfigurationManager<OneDasSettings>.Settings.BaseDirectoryPath))
            {
                ConfigurationManager<OneDasSettings>.Settings.BaseDirectoryPath = configurationDirectoryPath;
            }

            try
            {
                if (!string.Equals(Path.GetFullPath(ConfigurationManager<OneDasSettings>.Settings.BaseDirectoryPath), Path.GetFullPath(ConfigurationManager<OneDasSettings>.Settings.NewBaseDirectoryPath), StringComparison.OrdinalIgnoreCase))
                {
                    ConfigurationManager<OneDasSettings>.Settings.BaseDirectoryPath = ConfigurationManager<OneDasSettings>.Settings.NewBaseDirectoryPath;
                }
            }
            catch
            {
                ConfigurationManager<OneDasSettings>.Settings.NewBaseDirectoryPath = ConfigurationManager<OneDasSettings>.Settings.BaseDirectoryPath;
            }
            finally
            {
                ConfigurationManager<OneDasSettings>.Save();
            }

            // exception handling
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // privileges check
            WindowsPrincipal windowsPrincipal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            Bootloader.IsElevated = windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator) || windowsPrincipal.Claims.Any(x => x.Value == "S-1-5-18");

            // parse command line
            Bootloader.ParseCommandLineArgumentSet(args);

            // OneDasServiceController
            _oneDasServiceController = ServiceController.GetServices().FirstOrDefault(x => x.ServiceName == ConfigurationManager<OneDasSettings>.Settings.ApplicationName);

            // OneDasConsole or OneDasService
            if (Environment.UserInteractive)
            {
                if (Bootloader.GetOneDasServiceStatus() == 0)
                {
                    _oneDasController = new OneDasController();

                    Bootloader.CreateWebHost();

                    _task_WebHost = Task.Run(() =>
                    {
                        using (IWebHost webHost = _webhost)
                        {
                            _webhost.Run();
                        }
                    });
                }

                _oneDasConsole = new OneDasConsole();
            }
            else
            {
                ConfigurationManager<OneDasSettings>.Settings.IsAutostartEnabled = true;

                _oneDasController = new OneDasController();

                Bootloader.CreateWebHost();

                using (OneDasService oneDasService = new OneDasService(ConfigurationManager<OneDasSettings>.Settings.ApplicationName, _webhost))
                {
                    ServiceBase.Run(oneDasService);
                }
            }
        }

        public static void ParseCommandLineArgumentSet(IList<string> commandLineArgumentSet)
        {
            Contract.Requires(commandLineArgumentSet != null);

            if (commandLineArgumentSet.Contains("-REALTIME"))
            {
                Bootloader.BoostProcessPriority();
                Environment.Exit(0);
            }
            else if (commandLineArgumentSet.Contains("-INSTALLSERVICE"))
            {
                Bootloader.InstallOneDasService();
                Environment.Exit(0);
            }
            else if (commandLineArgumentSet.Contains("-UNINSTALLSERVICE"))
            {
                Bootloader.UninstallOneDasService();
                Environment.Exit(0);
            }
            else if (commandLineArgumentSet.Contains("-RESTARTSERVICE"))
            {
                Bootloader.RestartOneDasService();
                Environment.Exit(0);
            }
            else if (commandLineArgumentSet.Contains("-STOPSERVICE"))
            {
                Bootloader.StopOneDasService();
                Environment.Exit(0);
            }

            if (!commandLineArgumentSet.Contains("-ALLOWSINGELTON"))
            {
                if (!(Environment.UserInteractive && Bootloader.GetOneDasServiceStatus() > 0))
                {
                    if (!SingeltonHelper.EnsureSingeltonInstance(new Guid(ConfigurationManager<OneDasSettings>.Settings.MutexName), () => WindowHelper.BringWindowToFront(Process.GetCurrentProcess().MainWindowHandle)))
                    {
                        Environment.Exit(0);
                    }
                }
            }
        }

        public static ServiceControllerStatus GetOneDasServiceStatus()
        {
            if (_oneDasServiceController == null)
            {
                _oneDasServiceController = ServiceController.GetServices().FirstOrDefault(x => x.ServiceName == ConfigurationManager<OneDasSettings>.Settings.ApplicationName);
            }

            if (_oneDasServiceController != null)
            {
                _oneDasServiceController.Refresh();
                return _oneDasServiceController.Status;
            }
            else
            {
                return 0;
            }
        }

        public static void OnSystemMessageReceived(string message, string prefix = "")
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                Task.Run(() => Bootloader.SystemMessageReceived?.Invoke(null, new SystemMessageReceivedEventArgs($"(OneDAS) {message}")));
            }
            else
            {
                Task.Run(() => Bootloader.SystemMessageReceived?.Invoke(null, new SystemMessageReceivedEventArgs($"({prefix}) {message}")));
            }
        }

        #endregion

        #region "Process priority (elevated)"

        public static void BoostProcessPriority()
        {
            if (Process.GetCurrentProcess().PriorityClass != ProcessPriorityClass.RealTime)
            {
                CredUiHelper.ExecuteAsElevatedUser("-REALTIME", "OneDAS server process priority shall be set to realtime but the current user cannot be elevated. Please provide credentials to set the requested process priority.", () => Bootloader.InternalBoostProcessPriority());
            }
        }

        private static void InternalBoostProcessPriority()
        {
            Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).ToList().ForEach(x => x.PriorityClass = ProcessPriorityClass.RealTime);
        }

        #endregion

        #region "Service (elevated)"

        public static void InstallOneDasService()
        {
            CredUiHelper.ExecuteAsElevatedUser("-INSTALLSERVICE", "OneDAS server shall be installed as Windows service but the current user cannot be elevated. Please provide credentials to finish the installation.", () => Bootloader.InternalInstallOneDasService());
            _oneDasServiceController = ServiceController.GetServices().FirstOrDefault(x => x.ServiceName == ConfigurationManager<OneDasSettings>.Settings.ApplicationName);
        }

        private static void InternalInstallOneDasService()
        {
            using (AssemblyInstaller assemblyInstaller = new AssemblyInstaller(Assembly.GetExecutingAssembly(), null))
            {
                Hashtable savedState = new Hashtable();

                assemblyInstaller.UseNewContext = true;

                try
                {
                    assemblyInstaller.Install(savedState);
                    assemblyInstaller.Commit(savedState);
                }
                catch
                {
                    assemblyInstaller.Rollback(savedState);
                }
            }
        }

        public static void UninstallOneDasService()
        {
            CredUiHelper.ExecuteAsElevatedUser("-UNINSTALLSERVICE", "OneDAS server shall be uninstalled as Windows service but the current user cannot be elevated. Please provide credentials to finish the deinstallation.", () => Bootloader.InternalUninstallOneDasService());
            _oneDasServiceController = ServiceController.GetServices().FirstOrDefault(x => x.ServiceName == ConfigurationManager<OneDasSettings>.Settings.ApplicationName);
        }

        private static void InternalUninstallOneDasService()
        {
            using (AssemblyInstaller assemblyInstaller = new AssemblyInstaller(Assembly.GetExecutingAssembly(), null))
            {
                Hashtable savedState = new Hashtable();

                assemblyInstaller.UseNewContext = true;

                try
                {
                    assemblyInstaller.Uninstall(savedState);
                }
                catch
                {
                    assemblyInstaller.Rollback(savedState);
                }
            }
        }

        private static void RestartOneDasService()
        {
            CredUiHelper.ExecuteAsElevatedUser("-RESTARTSERVICE", "OneDAS server Windows service shall be restarted but the current user cannot be elevated. Please provide credentials to finish the installation.", () => Bootloader.InternalRestartOneDasService());
        }

        private static void InternalRestartOneDasService()
        {
            Process.Start("cmd.exe", $@"/C net stop ""{_oneDasServiceController.ServiceName}""&net start ""{_oneDasServiceController.ServiceName}""");
        }

        private static void StopOneDasService()
        {
            CredUiHelper.ExecuteAsElevatedUser("-STOPSERVICE", "OneDAS server Windows service shall be stopped but the current user cannot be elevated. Please provide credentials to finish the installation.", () => Bootloader.InternalStopOneDasService());
        }

        private static void InternalStopOneDasService()
        {
            Process.Start("cmd.exe", $@"/C net stop ""{_oneDasServiceController.ServiceName}""");
        }

        #endregion

        #region "Event log"

        public static void WriteLogEntry(string message, EventLogEntryType eventLogEntryType)
        {
            try
            {
                EventLog.WriteEntry(ConfigurationManager<OneDasSettings>.Settings.ApplicationName, message, eventLogEntryType);
            }
            catch
            {
                string folderPath = ConfigurationManager<OneDasSettings>.Settings.BaseDirectoryPath;

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                File.AppendAllText(Path.Combine(ConfigurationManager<OneDasSettings>.Settings.BaseDirectoryPath, "EventLog.txt"), $"{DateTime.UtcNow} - {eventLogEntryType.ToString()}: {message}{Environment.NewLine}");
            }
        }

        #endregion

        #region "Exception handling"

        public static Exception UnwrapException(Exception exception)
        {
            if (exception is TargetInvocationException)
            {
                return exception.InnerException;
            }

            return exception;
        }
       

        public static void HandleException(Exception exception)
        {
            exception = Bootloader.UnwrapException(exception);

            Bootloader.LastError = exception.Message;

            if (Bootloader.OneDasController != null)
            {
                Bootloader.OneDasController.OneDasState = OneDasState.Error;
            }

            try
            {
                Bootloader.WriteLogEntry(exception.ToString(), EventLogEntryType.Error);
            }
            catch
            {
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Bootloader.WriteLogEntry(e.ExceptionObject.ToString(), EventLogEntryType.Error);
            }
            catch
            {
            }
        }

        #endregion

        #region "WebHost"
        
        private static void CreateWebHost()
        {
            if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")))
            {
                Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            }

            _webhost = new WebHostBuilder()
                            .UseKestrel()
                            .UseUrls(ConfigurationManager<OneDasSettings>.Settings.AspBaseUrl)
                            .UseContentRoot(Directory.GetCurrentDirectory())
                            .UseStartup<Startup>()
                            //.UseApplicationInsights()
                            .Build();
        }

        #endregion

        #region "Shutdown"

        public static void Shutdown(bool restart, int returnCode)
        {
            Bootloader.Dispose();

            if (restart)
            {
                if (Environment.UserInteractive)
                {
                    ProcessStartInfo processStartInfo = new ProcessStartInfo
                    {
                        Arguments = "-ALLOWSINGELTON",
                        FileName = Assembly.GetExecutingAssembly().Location
                    };

                    Process process = new Process { StartInfo = processStartInfo };
                    process.Start();
                }
                else
                {
                    Bootloader.RestartOneDasService();
                }
            }

            if (Environment.UserInteractive)
            {
                Environment.Exit(returnCode);
            }
            else
            {
                Bootloader.StopOneDasService();
            }
        }

        #endregion

        #region "IDisposable Support"

        private static bool isDisposed;

        public static async void Dispose()
        {
            if (!isDisposed)
            {
                if (_oneDasController != null)
                {
                    Bootloader.WriteLogEntry("shutdown started", EventLogEntryType.Information);

                    _webhost?.StopAsync();
                    
                    if (Environment.UserInteractive)
                    {
                        await _task_WebHost;
                    }

                    Bootloader.OneDasController?.Dispose();

                    Bootloader.WriteLogEntry("shutdown finished", EventLogEntryType.Information);
                }
            }

            isDisposed = true;
        }

        #endregion
    }
}