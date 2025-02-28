﻿using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Qiqqa.Common;
using Qiqqa.Common.Configuration;
using Qiqqa.Common.GUI;
using Qiqqa.Common.MessageBoxControls;
using Qiqqa.DocumentLibrary.WebLibraryStuff;
using Qiqqa.Main.IPC;
using Qiqqa.Main.LoginStuff;
using Qiqqa.UpgradePaths;
using Utilities;
using Utilities.Files;
using Utilities.GUI;
using Utilities.GUI.DualTabbedLayoutStuff;
using Utilities.Misc;
using Utilities.ProcessTools;
using Utilities.Shutdownable;
using Console = Utilities.GUI.Console;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;


#if CEFSHARP
using CefSharp.Wpf;
using CefSharp;
#endif


namespace Qiqqa.Main
{
    public static class MainEntry
    {
        [DllImport("kernel32.dll")]
        private static extern int SetErrorMode(int newMode);

        private static Application application;

        static MainEntry()
        {
            try
            {
                DoPreamble();
                DoApplicationCreate();

                SafeThreadPool.QueueUserWorkItem(() =>
                {
                    //DoUpgrades();  -- delay doing updates until we have had the 'login' dialog where we show and possibly *change* the base directory!

#if false 									// set to true for testing the UI behaviour wile this takes a long time to 'run':
                    Thread.Sleep(15000);
#endif
                    DoPostInit();
                });
            }
            catch (Exception ex)
            {
                MessageBoxes.Error(ex, "There was an exception in the top-level static main entry!");
            }
        }

        private static void DoPreamble()
        {
            // Make sure the temp directory exists
            if (!TempDirectoryCreator.CheckTempExists())
            {
                MessageBoxes.Error(@"Qiqqa needs the directory {0} to exist for it to function properly.  Please create it or set the TEMP environment variable and restart Qiqqa.", TempFile.TempDirectoryForQiqqa);
            }

            // Make sure that windir is set (goddamned font subsystem bug: http://stackoverflow.com/questions/10094197/wpf-window-throws-typeinitializationexception-at-start-up)
            {
                if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable("windir")))
                {
                    Logging.Warn("Environment variable windir is empty so setting it to {0}", Environment.GetEnvironmentVariable("SystemRoot"));
                    Environment.SetEnvironmentVariable("windir", Environment.GetEnvironmentVariable("SystemRoot"));
                }
            }

            Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture;

            Thread.CurrentThread.Name = "Main";

            // Check if we're running in "Portable Application" mode:
#if false
            {
                string[] app_paths = new string[] {
                    Path.Combine(UnitTestDetector.StartupDirectoryForQiqqa, @"x"),
                    Path.Combine(System.AppContext.BaseDirectory, @"x"),
                    Process.GetCurrentProcess().MainModule.FileName,
                    new Uri(System.Reflection.Assembly.GetExecutingAssembly().Location).LocalPath,
                    new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath,
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"x"),
                    Environment.GetCommandLineArgs()[0]
                };

                foreach (string app_path in app_paths)
                {
                    string p = Path.GetFullPath(app_path);
                    p = Path.GetDirectoryName(p);
                    p = Path.Combine(p, @"Qiqqa.Portable.Settings.json5");
                    if (File.Exists(p))
                    {
                        Logging.Info(p);
                    }
                }
            }
#endif

            UserRegistry.DetectPortableApplicationMode();

            {
                if (UserRegistry.GetPortableApplicationMode())
                {
                    // set up defaults when they are absent:
                    object v = null;
                    UserRegistry.DeveloperOverridesDB.TryGetValue("BaseDataDirectory", out v);
                    if (string.IsNullOrEmpty(v as string))
                    {
                        UserRegistry.DeveloperOverridesDB.Add("BaseDataDirectory", Path.GetFullPath(Path.Combine(UnitTestDetector.StartupDirectoryForQiqqa, @"../My.Qiqqa.Libraries")));
                    }
                }
            }

            // now also check for a developer override config file in the Basedirectory and add those overrides to the set:
            RegistrySettings.AugmentDeveloperOverridesDB();

            if (RegistrySettings.Instance.IsSet(RegistrySettings.DebugConsole))
            {
                Console.Instance.Init();
                Logging.Info("Console initialised");
            }

            // Support windows-level error reporting - helps suppressing the errors in pdfdraw.exe and QiqqaOCR.exe
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ms680621%28v=vs.85%29.aspx
            try
            {
                SetErrorMode(0x0001 | 0x0002 | 0x0004 | 0x8000);
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "Error trying to suppress global process failure.");
            }

            // kick the number of threads in the threadpool down to a reasonable number
            SafeThreadPool.SetMaxActiveThreadCount();

            AppDomain.CurrentDomain.AssemblyLoad += delegate (object sender, AssemblyLoadEventArgs args)
            {
                Logging.Info("Loaded assembly: {0}", args.LoadedAssembly.FullName);
                Logging.TriggerInit();
            };

#if CEFSHARP

#region CEFsharp setup

            // CEFsharp setup for AnyPC as per https://github.com/cefsharp/CefSharp/issues/1714:
            AppDomain.CurrentDomain.AssemblyResolve += CefResolver;

            InitCef();

#endregion CEFsharp setup

#endif

            try
            {
                FirstInstallWebLauncher.Check();
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "Unknown exception during FirstInstallWebLauncher.Check().");
            }

            try
            {
                FileAssociationRegistration.DoRegistration();
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "Unknown exception during FileAssociationRegistration.DoRegistration().");
            }

            // Start tracing WPF events
#if DEBUG
            WPFTrace wpf_trace = new WPFTrace();
            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Listeners.Add(wpf_trace);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            System.Diagnostics.Trace.AutoFlush = true;
#endif

            // If we have a command line parameter and another instance is running, send it to them and exit
            string[] command_line_args = Environment.GetCommandLineArgs();
            if (1 < command_line_args.Length && !ProcessSingleton.IsProcessUnique(false))
            {
                IPCClient.SendMessage(command_line_args[1]);
                Environment.Exit(-2);
            }

            // Check that we are the only instance running
            try
            {
                if (!RegistrySettings.Instance.IsSet(RegistrySettings.AllowMultipleQiqqaInstances) && !ProcessSingleton.IsProcessUnique(bring_other_process_to_front_if_it_exists: true))
                {
                    MessageBoxes.Info("There seems to be an instance of Qiqqa already running so Qiqqa will not start again.\n\nSometimes it takes a few moments for Qiqqa to exit as it finishes up a final OCR or download.  If this problem persists, you can kill the Qiqqa.exe process in Task Manager.");
                    Logging.Info("There is another instance of Qiqqa running, so exiting.");
                    Environment.Exit(-1);
                }
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "Unknown exception while checking for single app instance.  Continuing as normal so there could be several Qiqqas running.");
            }

            ComputerStatistics.LogCommonStatistics(ConfigurationManager.GetCurrentConfigInfos());
        }

        private static void DoApplicationCreate()
        {
            // Create the application object
            application = new Application();
            application.ShutdownMode = ShutdownMode.OnLastWindowClose;
            application.Exit += Application_Exit;
            application.Activated += Application_Activated;
            application.LoadCompleted += Application_LoadCompleted;
            application.Startup += Application_Startup;
            application.SessionEnding += Application_SessionEnding;

            // DO NOT set up all the exception handling when we're running in a Developer Environment, i.e. when we were attached to a debugger, such as Visual Studio:
            if (!Debugger.IsAttached)
            {
                application.DispatcherUnhandledException += application_DispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                SafeThreadPool.UnhandledException += SafeThreadPool_UnhandledException;
            }

            Process proc = Process.GetCurrentProcess();
            //     Occurs each time an application writes a line to its redirected System.Diagnostics.Process.StandardOutput/StandardError
            proc.ErrorDataReceived += Proc_ErrorDataReceived;
            proc.OutputDataReceived += Proc_OutputDataReceived;
            //     Occurs when a process exits.
            proc.Exited += Proc_Exited;
            proc.EnableRaisingEvents = true;

            // Start the FPS measurer
            { var init = WPFFrameRate.Instance; }
        }

        private static void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            Logging.Warn($"---Application::SessionEnding: {e.ReasonSessionEnding}");

            SignalShutdown($"Windows Session ending: {e.ReasonSessionEnding}");
        }

        private static void Proc_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Logging.Warn($"---Application::StdErr:\n{e.Data}");
        }

        private static void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Logging.Warn($"---Application::StdOut:\n{e.Data}");
        }

        private static void Proc_Exited(object sender, EventArgs e)
        {
            Logging.Info("---Application/Process::Exit invoked");

            SignalShutdown("Main Application/Process::Exit event");
        }

        private static void Application_Startup(object sender, StartupEventArgs e)
        {
            Logging.Info("---Application_Startup");
        }

        private static void Application_LoadCompleted(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            Logging.Info("---Application_LoadCompleted");
        }

        private static void Application_Activated(object sender, EventArgs e)
        {
            Logging.Info("---Application_Activated");
        }

        private static void Application_Exit(object sender, ExitEventArgs e)
        {
            Logging.Info("---Application::Exit - user (probably) closed main/last open window.");

            SignalShutdown("Main Application::Exit event - user (probably) closed main/last open window.");
        }

#if false
        private static void DoUpgrades()
        {
            // Perform any upgrade paths that we must
            UpgradeManager.RunUpgrades();
        }
#endif

        private static void DoPostInit()
        {
            WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

            // NB NB NB NB: You CANT USE ANYTHING IN THE USER CONFIG AT THIS POINT - it is not yet decided until LOGIN has completed...

            WPFDoEvents.InvokeInUIThread(() =>
            {
                StatusManager.Instance.UpdateStatus("AppStart", "Loading themes");
                Theme.Initialize();
                DualTabbedLayout.GetWindowOverride = delegate () {
                    return new StandardWindow();
                };

                // Force tooltips to stay open
                ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(3600000));
            });

            // and kick off the Login Dialog to start the application proper:
            WPFDoEvents.InvokeAsyncInUIThread(() => ShowLoginDialog());

#if DEBUG
            // regression test the error catching the various sync and async handlers...
            WPFDoEvents.TestAsyncErrorHandling();
#endif

            // NB NB NB NB: You CANT USE ANYTHING IN THE USER CONFIG AT THIS POINT - it is not yet decided until LOGIN has completed...
        }

        private static void DoPostUpgrade()
        {
            WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

            // Make sure the data directories exist...
            Directory.CreateDirectory(ConfigurationManager.Instance.BaseDirectoryForUser);
        }

        public static void SignalShutdown(string reason)
        {
            ShutdownableManager.Instance.Shutdown(reason);
        }

        public static void ShowLoginDialog()
        {
            LoginWindow login_window = new LoginWindow();
            login_window.ChooseLogin();
        }

        private static int log_close_down_counter = 2;  // 2: once at end of main, plus once at (hopefully) end of threadpool queue.

        public static void CloseLogFile()
        {
            ComputerStatistics.ReportMemoryStatus($"Status at termination end stage {3 - log_close_down_counter}");

            // only close the log when this function has been called TWICE:
            log_close_down_counter--;
            if (log_close_down_counter == 0)
            {
                // This must be the last line the application executes, EVAR!
                Logging.ShutDown();
            }
        }

        [STAThread]
        private static void Main()
        {
            Logging.Info("+static Main()");

            try
            {
                StatusManager.Instance.UpdateStatus("AppStart", "Logging in");

                // NOTE: the initial Login Dialog will be shown by code at the end
                // of the (background) DoPostUpgrade() process which is already running
                // by the time we arrive at this location.
                //
                // This ensures all process parts, which are expected to be done by
                // the time to login Dialog is visible (and usable by the user), are
                // indeed ready.

                Exception has_ex = null;
                try
                {
                    application.Run();
                }
                catch (Exception ex)
                {
                    has_ex = ex;
                    Logging.Error(ex, "Exception caught at Main() Application::Run().  Disaster.");
                }

                SignalShutdown(has_ex != null ? $"Exception caught in Main Application::Run() function: {has_ex}" : "Main Application::Run() has terminated.");
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "Exception caught at Main().  Disaster.");

                SignalShutdown($"Exception caught in Main Application function: {ex}");
            }

            Logging.Info("-static Main()");

            // When we get here and wait for the other threads to close off any business they're attending,
            // we SHOULD have a nicely terminated application run and all the heap allocations left should
            // be MEMORY LEAKS.
            // BUT that's only going to be anywhere near TRUE when we make sure we discard the loaded
            // libraries all properly like, etc.
            // So that's what we're going to do next. And forced GC to kick the sluggish off the lot
            // before we report.
            Logging.Info("Unloading all user libraries...");

            // kick off all the libraries
            WebLibraryManager.Instance.UnloadAllLibraries();

            SafeThreadPool.QueueUserWorkItem(CloseLogFile, skip_task_at_app_shutdown: false);

            Logging.Info("Making sure all threads have completed or terminated...");

            // give this a sane upper limit so the application cannot ever be 'stuck in the background' due to this:
            int wait_time = 5000;
            for (int min_rounds = 3;
                wait_time > 0 &&
                (
                    min_rounds > 0 ||
                    SafeThreadPool.RunningThreadCount > 0 ||
                    GC.GetTotalMemory(false) > 10000000L ||
                    ComputerStatistics.GetTotalRunningThreadCount() > 0
                );
                min_rounds--)
            {
                GC.WaitForPendingFinalizers();
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                Logging.Info($"-static Heap after forced GC compacting at the end (in the wait-for-all-threads-to-terminate loop): {GC.GetTotalMemory(false)} Bytes, {SafeThreadPool.RunningThreadCount} tasks active, {ComputerStatistics.GetTotalRunningThreadCount()} threads running");

                Thread.Sleep(1000);
                wait_time -= 1000;
            }
            Logging.Info($"Last machine state observation before shutting down the log at the very end of the application run: Heap after forced GC compacting: {GC.GetTotalMemory(false)} Bytes, {SafeThreadPool.RunningThreadCount} tasks active, {ComputerStatistics.GetTotalRunningThreadCount()} threads running, {wait_time / 1000} seconds overtime unused (more than zero for this one is good!)");

            CloseLogFile();
        }

        private static void SafeThreadPool_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            RemarkOnException(e.ExceptionObject as Exception, potentially_fatal: false);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            bool obnoxious_but_not_fatal = ex.Message.Contains(" GDI+") // "generic error happenedinGDI+."
                || ex.Message.Contains("while rasterising");            // SORAX: Error while rasterising page 24 at 456.6737dpi of 'G:\\Qiqqa\\base\\INTRANET_A863F89A-6729-49FD-9DDE-AEA03E698867\\documents\\1\\17C861BF0298B776E7A199D0F09C9BFF5E7037.pdf'
            if (ex is NotImplementedException)
            {
                obnoxious_but_not_fatal = true;   // Reports about stuff that's not yet implemented are harmless
            }
            RemarkOnException(ex, !obnoxious_but_not_fatal);
        }

        private static void application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Exception ex = e.Exception;
            bool obnoxious_but_not_fatal = ex.Message.Contains(" GDI+");   // "generic error happenedinGDI+."
            if (ex is NotImplementedException)
            {
                obnoxious_but_not_fatal = true;   // Reports about stuff that's not yet implemented are harmless
            }
            RemarkOnException(ex, !obnoxious_but_not_fatal);
            e.Handled = true;
        }

        private static void RemarkOnException(Exception ex, bool potentially_fatal)
        {
            Logging.Error(ex, "RemarkOnException.....");

            if (!ShutdownableManager.Instance.IsShuttingDown)
            {
                WPFDoEvents.InvokeAsyncInUIThread(() =>
                {
                    RemarkOnException_GUI_THREAD(ex, potentially_fatal);
                }
                );
            }
        }

        private static void RemarkOnException_GUI_THREAD(Exception ex, bool potentially_fatal)
        {
            try
            {
                Logging.Error(ex, "RemarkOnException_GUI_THREAD...");

                // the garbage collection is not crucial for the functioning of the dialog itself, hence dump it into a worker thread.
                SafeThreadPool.QueueUserWorkItem(() =>
                {
                    // Collect all generations of memory.
                    GC.WaitForPendingFinalizers();
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                });

                bool isGDIfailureInXULrunner = ex.Message.Contains("A generic error occurred in GDI+");
                const int EACCESS = unchecked((int)0x80004005);
                if (isGDIfailureInXULrunner || ex.HResult == EACCESS)
                {
                    potentially_fatal = false;
                }

                // do NOT display GDI+ errors as they are merely obnoxious and unresolvable anyway:
                if (!isGDIfailureInXULrunner)
                {
                    UnhandledExceptionMessageBox.DisplayException(ex);
                }
            }
            catch (Exception ex2)
            {
                Logging.Error(ex2, "Exception thrown in top level error handler!!");
            }

            if (potentially_fatal)
            {
                // signal the application to shutdown as an unhandled exception is a grave issue and nothing will be guaranteed afterwards.
                ShutdownableManager.Instance.Shutdown($"Exception received in top level error handler: {ex}");

                // and terminate the Windows Message Loop if it hasn't already (in my tests, Qiqqa was stuck in there without a window to receive messages from at this point...)
                MainWindowServiceDispatcher.Instance.ShutdownQiqqa(suppress_exit_warning: true);
            }
        }

#if CEFSHARP

#region CEFsharp setup helpers

        // CEFsharp setup code as per https://github.com/cefsharp/CefSharp/issues/1714:

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitCef()
        {
            var settings = new CefSettings();

            // Set BrowserSubProcessPath based on app bitness at runtime
            settings.BrowserSubprocessPath = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
                                                   Environment.Is64BitProcess ? "x64" : "x86",
                                                   "CefSharp.BrowserSubprocess.exe");

            // Make sure you set performDependencyCheck false
            Cef.Initialize(settings, performDependencyCheck: false, browserProcessHandler: null);

#if false
            var browser = new BrowserForm();
            Application.Run(browser);
#endif
        }

        // Will attempt to load missing assembly from either x86 or x64 subdir
        private static Assembly CefResolver(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("CefSharp"))
            {
                string assemblyName = args.Name.Split(new[] { ',' }, 2)[0] + ".dll";
                string archSpecificPath = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
                                                       Environment.Is64BitProcess ? "x64" : "x86",
                                                       assemblyName);

                return File.Exists(archSpecificPath)
                           ? Assembly.LoadFile(archSpecificPath)
                           : null;
            }

            return null;
        }

#endregion CEFsharp setup helpers

#endif

    }
}
