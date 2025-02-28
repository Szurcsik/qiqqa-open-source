﻿using System;
using System.Threading;
using icons;
using Qiqqa.ClientVersioning;
using Qiqqa.Common.Configuration;
using Qiqqa.DocumentLibrary;
using Qiqqa.DocumentLibrary.AutoSyncStuff;
using Qiqqa.DocumentLibrary.BundleLibrary.BundleLibraryDownloading;
using Qiqqa.DocumentLibrary.Import.Auto;
using Qiqqa.DocumentLibrary.MetadataExtractionDaemonStuff;
using Qiqqa.DocumentLibrary.WebLibraryStuff;
using Qiqqa.Main;
using Qiqqa.Marketing;
using Qiqqa.Synchronisation.PDFSync;
using Utilities;
using Utilities.Maintainable;

namespace Qiqqa.Common.BackgroundWorkerDaemonStuff
{
    public class BackgroundWorkerDaemon
    {
        public static readonly BackgroundWorkerDaemon Instance = new BackgroundWorkerDaemon();
        private MetadataExtractionDaemon metadata_extraction_daemon;

        private BackgroundWorkerDaemon()
        {
            Logging.Info("Starting background worker daemon.");

            metadata_extraction_daemon = new MetadataExtractionDaemon();

            MaintainableManager.Instance.RegisterHeldOffTask(DoMaintenance_OnceOff, 1 * 1000, hold_off_level: 1);
            MaintainableManager.Instance.RegisterHeldOffTask(DoMonitoring_Frequent, 0, 2 * 1000);
            MaintainableManager.Instance.RegisterHeldOffTask(DoMaintenance_Frequent, 10 * 1000, 1 * 1000);
            MaintainableManager.Instance.RegisterHeldOffTask(DoMaintenance_Infrequent, 10 * 1000, 10 * 1000);
            MaintainableManager.Instance.RegisterHeldOffTask(DoMaintenance_QuiteInfrequent, 10 * 1000, 1 * 60 * 1000);
            MaintainableManager.Instance.RegisterHeldOffTask(DoMaintenance_VeryInfrequent, 10 * 1000, 15 * 60 * 1000);

            // hold off: level 3 -> 2
            MaintainableManager.Instance.BumpHoldOffPendingLevel();
        }

        private void DoMaintenance_OnceOff(Daemon daemon)
        {
            Logging.Debug特("DoMaintenance_OnceOff START");

            if (daemon.StillRunning)
            {
                // KICK THEM OFF

                try
                {
                    StartupCommandLineParameterChecker.Check();
                }
                catch (Exception ex)
                {
                    Logging.Error(ex, "Exception during StartupCommandLineParameterChecker.Check");
                }

                InitClientUpdater();

                try
                {
                    AlternativeToReminderNotification.CheckIfWeWantToNotify();
                }
                catch (Exception ex)
                {
                    Logging.Error(ex, "Exception during AlternativeToReminderNotification.CheckIfWeWantToNotify");
                }

                try
                {
                    DropboxChecker.DoCheck();
                }
                catch (Exception ex)
                {
                    Logging.Error(ex, "Exception during DropboxChecker.DoCheck");
                }

                try
                {
                    AutoImportFromMendeleyChecker.DoCheck();
                }
                catch (Exception ex)
                {
                    Logging.Error(ex, "Exception during AutoImportFromMendeleyChecker.DoCheck");
                }

                try
                {
                    AutoImportFromEndnoteChecker.DoCheck();
                }
                catch (Exception ex)
                {
                    Logging.Error(ex, "Exception during AutoImportFromEndnoteChecker.DoCheck");
                }

                // hold off: level 1 -> 0
                MaintainableManager.Instance.BumpHoldOffPendingLevel();
            }

            // We only want this to run once
            daemon.Stop();
        }

        public void InitClientUpdater()
        {
            if (null == ClientUpdater.Instance)
            {
                Logging.Warn("TODO: Checking for updates: check the github releases page(s) and report back to the user there's an update available.");

                try
                {
                    ClientUpdater.Init("Qiqqa",
                                       Icons.Upgrade,
                                       WebsiteAccess.GetOurFileUrl(WebsiteAccess.OurSiteFileKind.ClientVersion),
                                       WebsiteAccess.GetOurFileUrl(WebsiteAccess.OurSiteFileKind.ClientSetup),
                                       WebsiteAccess.IsTestEnvironment,
                                       ShowReleaseNotes);

                    ClientUpdater.Instance.CheckForNewClientVersion(ConfigurationManager.Instance.Proxy);
                }
                catch (Exception ex)
                {
                    Logging.Error(ex, "Exception during Utilities.ClientVersioning.ClientUpdater.Instance.CheckForNewClientVersion");
                }
            }
        }

        private void ShowReleaseNotes(string release_notes)
        {
            Logging.Info("Release Notes: {0}", release_notes);
            new ClientVersionReleaseNotes(release_notes).ShowDialog();
        }

        private void DoMaintenance_VeryInfrequent(Daemon daemon)
        {
            try
            {
                Logging.Debug特("DoMaintenance_VeryInfrequent START");

                if (ConfigurationManager.Instance.ConfigurationRecord.DisableAllBackgroundTasks)
                {
                    Logging.Debug特("Daemons are forced to sleep via Configuration::DisableAllBackgroundTasks");
                    return;
                }

                try
                {
                    AutoSyncManager.Instance.DoMaintenance();
                }
                catch (Exception ex)
                {
                    Logging.Error(ex, "Exception in autosync_manager_daemon");
                }

                // If this library is busy, skip it for now
                if (Library.IsBusyAddingPDFs)
                {
                    Logging.Debug特("DoMaintenance_VeryInfrequent: Not daemon processing any library that is busy with adds...");
                    return;
                }

                foreach (var web_library_detail in WebLibraryManager.Instance.WebLibraryDetails_WorkingWebLibraries)
                {
                    if (web_library_detail.IsBundleLibrary)
                    {
                        try
                        {
                            BundleLibraryUpdatedManifestChecker.Check(web_library_detail);
                        }
                        catch (Exception ex)
                        {
                            Logging.Warn(ex, "Exception in BundleLibraryUpdatedManifestChecker.Check()");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "Terminating the Very Infrequent Maintenance background thread due to an otherwise unhandled exception.");
            }
        }

        private void DoMaintenance_QuiteInfrequent(Daemon daemon)
        {
            try
            {
                Logging.Debug特("DoMaintenance_QuiteInfrequent START");

                if (ConfigurationManager.Instance.ConfigurationRecord.DisableAllBackgroundTasks)
                {
                    Logging.Debug特("Daemons are forced to sleep via Configuration::DisableAllBackgroundTasks");
                    return;
                }

                // If this library is busy, skip it for now
                if (Library.IsBusyAddingPDFs)
                {
                    Logging.Debug特("DoMaintenance_QuiteInfrequent: Not daemon processing any library that is busy with adds...");
                    return;
                }
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "Terminating the Quite-Infrequent Maintenance background thread due to an otherwise unhandled exception.");
            }
        }

        private void DoMaintenance_Infrequent(Daemon daemon)
        {
            try
            {
                Logging.Debug特("DoMaintenance_Infrequent START");

                if (ConfigurationManager.Instance.ConfigurationRecord.DisableAllBackgroundTasks)
                {
                    Logging.Debug特("Daemons are forced to sleep via Configuration::DisableAllBackgroundTasks");
                    return;
                }

                // If this library is busy, skip it for now
                if (Library.IsBusyAddingPDFs)
                {
                    Logging.Debug特("DoMaintenance_Infrequent: Not daemon processing any library that is busy with adds...");
                    return;
                }

                if (!ConfigurationManager.IsEnabled("BuildSearchIndex"))
                {
                    Logging.Debug特("DoMaintenance_Infrequent::IncrementalBuildIndex: Breaking out of processing loop due to BuildSearchIndex=false");
                    return;
                }

                foreach (var web_library_detail in WebLibraryManager.Instance.WebLibraryDetails_WorkingWebLibraries)
                {
                    if (WebLibraryDetail.LibraryIsLoaded(web_library_detail))
                    {
                        continue;
                    }

                    Library library = web_library_detail.Xlibrary;

                    try
                    {
                        metadata_extraction_daemon.DoMaintenance(web_library_detail, () =>
                        {
                            try
                            {
                                library.LibraryIndex?.IncrementalBuildIndex(web_library_detail);
                            }
                            catch (Exception ex)
                            {
                                Logging.Error(ex, "Exception in LibraryIndex.IncrementalBuildIndex()");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Logging.Error(ex, "Exception in metadata_extraction_daemon");
                    }
                }

                Logging.Debug特("DoMaintenance_Infrequent END");
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "Terminating the Infrequent Maintenance background thread due to an otherwise unhandled exception.");
            }
        }

        private void DoMaintenance_Frequent(Daemon daemon)
        {
            try
            {
                if (ConfigurationManager.Instance.ConfigurationRecord.DisableAllBackgroundTasks)
                {
                    Logging.Debug特("Daemons are forced to sleep via Configuration::DisableAllBackgroundTasks");
                    return;
                }

                // If this library is busy, skip it for now
                if (Library.IsBusyAddingPDFs)
                {
                    Logging.Debug特("DoMaintenance_Frequent: Not daemon processing any library that is busy with adds...");
                    return;
                }

                // Check for new syncing
                try
                {
                    SyncQueues.Instance.DoMaintenance(daemon);
                }
                catch (Exception ex)
                {
                    Logging.Error(ex, "Exception in SyncQueues.Instance.DoMaintenance");
                }
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "Terminating the Frequent Maintenance background thread due to an otherwise unhandled exception.");
            }
        }

        private void DoMonitoring_Frequent(Daemon daemon)
        {
            try
            {
                ActivityMonitorCore.BackgroundTask();
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "Terminating the FrequentMonitoring Background Thread due to an otherwise unhandled exception.");
            }
        }
    }
}
