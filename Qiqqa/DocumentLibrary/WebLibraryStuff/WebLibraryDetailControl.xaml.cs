﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using icons;
using Qiqqa.Common;
using Qiqqa.Common.Configuration;
using Qiqqa.Documents.Common;
using Qiqqa.Documents.PDF;
#if SYNCFUSION_ANTIQUE
using Syncfusion.Windows.Chart;
#endif
using Utilities;
using Utilities.Collections;
using Utilities.DateTimeTools;
using Utilities.GUI;
using Utilities.GUI.Wizard;
using Utilities.Images;
using Utilities.Misc;
using Brushes = System.Drawing.Brushes;
using Cursors = System.Windows.Input.Cursors;
using Image = System.Windows.Controls.Image;
using Matrix = System.Drawing.Drawing2D.Matrix;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using System.Windows.Threading;

namespace Qiqqa.DocumentLibrary.WebLibraryStuff
{
    /// <summary>
    /// Interaction logic for WebLibraryDetailControl.xaml
    /// </summary>
    public partial class WebLibraryDetailControl : DockPanel
    {
        private static int PREVIEW_IMAGE_HEIGHT = 350;
        private static int PREVIEW_IMAGE_WIDTH = 350;
        private static double PREVIEW_IMAGE_PERCENTAGE = .4;
        private WebLibraryListControl.WebLibrarySelectedDelegate web_library_selected_delegate;
        private DragToLibraryManager drag_to_library_manager;
        private bool concise_view = false;

        private static readonly string READONLY_BLURB = "You have only read-only access to this library.  Any changes or annotations you make are likely to be overwritten the next time you sync.";
        private const double BACKGROUND_IMAGE_OPACITY_INACTIVE = 0.1;
        private const double BACKGROUND_IMAGE_OPACITY_ACTIVE = 0.3;

        public WebLibraryDetailControl(bool concise_view, bool open_cover_flow, WebLibraryListControl.WebLibrarySelectedDelegate web_library_selected_delegate)
        {
            this.concise_view = concise_view;
            this.web_library_selected_delegate = web_library_selected_delegate;

            Theme.Initialize();

            InitializeComponent();

            ButtonMain.Background = ThemeColours.Background_Brush_Blue_LightToDark;

            //RenderOptions.SetBitmapScalingMode(ObjTitleImage, BitmapScalingMode.HighQuality);
            ObjTitleImage.Opacity = BACKGROUND_IMAGE_OPACITY_INACTIVE;
            ObjTitleImage.Stretch = Stretch.UniformToFill;

            ObjTitlePanel.Cursor = Cursors.Hand;
            ObjTitlePanel.MouseLeftButtonUp += Button_MouseLeftButtonUp;
            ObjTitlePanel.MouseRightButtonUp += ObjTitlePanel_MouseRightButtonUp;
            ObjTitlePanel.Background = ThemeColours.Background_Brush_Blue_VeryDarkToDark;
            ObjTitlePanel.MouseEnter += ObjTitlePanel_MouseEnter;
            ObjTitlePanel.MouseLeave += ObjTitlePanel_MouseLeave;
            ObjTitlePanel.ToolTip = "Click to open this library.";

            HyperlinkEdit.OnClick += HyperlinkEdit_OnClick;
            HyperlinkDelete.OnClick += HyperlinkDelete_OnClick;
            HyperlinkPurge.OnClick += HyperlinkPurge_OnClick;
            HyperlinkForget.OnClick += HyperlinkForget_OnClick;
            HyperlinkLocateSyncPoint.OnClick += HyperlinkLocateSyncPoint_OnClick;

            TxtTitle.FontSize = ThemeColours.HEADER_FONT_SIZE;
            TxtTitle.FontFamily = ThemeTextStyles.FontFamily_Header;

            ButtonReadOnly.Icon = Icons.GetAppIcon(Icons.WebLibrary_ReadOnly);
            ButtonReadOnly.Click += ButtonReadOnly_Click;
            ButtonReadOnly.ToolTip = READONLY_BLURB;

            ButtonAutoSync.Icon = Icons.GetAppIcon(Icons.WebLibrary_AutoSync);
            ButtonAutoSync.Click += ButtonAutoSync_Click;
            ButtonAutoSync.ToolTip = "Click here to turn on auto-syncing for this library.";

            ButtonCharts.Icon = Icons.GetAppIcon(Icons.WebLibrary_Charts);
            ButtonCharts.Click += ButtonCharts_Click;
            ButtonCharts.ToolTip = "Click here to see your recent activity in this library.";

            ButtonCoverFlow.Icon = Icons.GetAppIcon(Icons.WebLibrary_CoverFlow);
            ButtonCoverFlow.Click += ButtonCoverFlow_Click;
            ButtonCoverFlow.ToolTip = "Click here to view recommended reading for this library.";

#if SYNCFUSION_ANTIQUE
            ObjCarousel.Cursor = Cursors.Hand;
            ObjCarousel.SizeChanged += ObjCarousel_SizeChanged;
            ObjCarousel.MouseDoubleClick += ObjCarousel_MouseDoubleClick;
            ObjCarousel.ToolTip = "Double-click to open this suggested document.\n\nFor your convenience, this region will also show any documents whose reading stage you set to:\n'Top Priority', 'Read Again' or 'Interrupted'.";
            ObjCarousel.Height = PREVIEW_IMAGE_HEIGHT;
#endif

            if (open_cover_flow)
            {
                ButtonCoverFlow.IsChecked = true;
            }

            drag_to_library_manager = new DragToLibraryManager(null);
            drag_to_library_manager.RegisterControl(this);

            UpdateLibraryStatistics();

            //  DispatcherTimer setup
            var dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += WeakEventHandler2.Wrap(dispatcherTimer_Tick, (eh) =>
            {
                dispatcherTimer.Tick -= eh;
            });
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(Constants.UI_REFRESH_POLLING_INTERVAL);
            dispatcherTimer.Start();
        }

        private long library_change_marker_tick = 0;

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            WPFDoEvents.SafeExec(() =>
            {
                WebLibraryDetail web_library_detail = DataContext as WebLibraryDetail;
                if (null != web_library_detail)
                {
                    PDFDocument doc = null;
                    if (web_library_detail.Xlibrary?.CheckIfDocumentsHaveChanged(ref library_change_marker_tick, ref doc) ?? false)
                    {
                        UpdateLibraryStatistics();
                    }
                }
            });
        }

        private void ButtonReadOnly_Click(object sender, RoutedEventArgs e)
        {
            ButtonReadOnly.IsChecked = false;
            MessageBoxes.Info(READONLY_BLURB);
        }

        private void ButtonAutoSync_Click(object sender, RoutedEventArgs e)
        {
            WebLibraryDetail web_library_detail = DataContext as WebLibraryDetail;
            if (null != web_library_detail)
            {
                web_library_detail.AutoSync = ButtonAutoSync.IsChecked ?? false;
                WebLibraryManager.Instance.NotifyOfChangeToWebLibraryDetail();
            }
        }

        private void ButtonCoverFlow_Click(object sender, RoutedEventArgs e)
        {
            UpdateLibraryStatistics();
        }

        private void ButtonCharts_Click(object sender, RoutedEventArgs e)
        {
            UpdateLibraryStatistics();
        }

        private void ObjTitlePanel_MouseLeave(object sender, MouseEventArgs e)
        {
            ObjTitleImage.Opacity = BACKGROUND_IMAGE_OPACITY_INACTIVE;
            ObjTitlePanel.Background = ThemeColours.Background_Brush_Blue_VeryDarkToDark;
        }

        private void ObjTitlePanel_MouseEnter(object sender, MouseEventArgs e)
        {
            ObjTitleImage.Opacity = BACKGROUND_IMAGE_OPACITY_ACTIVE;
            ObjTitlePanel.Background = ThemeColours.Background_Brush_Blue_LightToVeryLight;

        }

        private void ObjChartArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
#if SYNCFUSION_ANTIQUE
            if (ObjCarousel.Items.Count > 0)
            {
                ObjCarousel.SelectedIndex = (ObjCarousel.SelectedIndex + 1) % ObjCarousel.Items.Count;
            }
#endif
            e.Handled = true;
        }

        private void ObjCarousel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
#if SYNCFUSION_ANTIQUE
            ObjCarousel.RadiusX = ObjCarousel.ActualWidth * 0.5 * 0.8;
#endif
        }

        private void Button_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            OpenLibrary();
            e.Handled = true;
        }

        private void ObjTitlePanel_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            WebLibraryDetailControlPopup popup = new WebLibraryDetailControlPopup(this);
            popup.Open();
            e.Handled = true;
        }

        internal void OpenLibrary()
        {
            WebLibraryDetail web_library_detail = DataContext as WebLibraryDetail;
            if (null != web_library_detail)
            {
                web_library_selected_delegate(web_library_detail);
            }
            else
            {
                Logging.Error("Can't invoke execute when we are not bound to a WebLibraryDetail");
            }
        }

        private void HyperlinkPurge_OnClick(object sender, MouseButtonEventArgs e)
        {
            WebLibraryDetail web_library_detail = DataContext as WebLibraryDetail;

            if (MessageBoxes.AskQuestion("Are you sure you want to purge the deleted library '{0}'?  All information you have in it will be lost forever!", web_library_detail.Title))
            {
                if (MessageBoxes.AskQuestion("One more time just to be sure: are you sure you want to purge the deleted library '{0}'?  All information you have in it will be lost forever!", web_library_detail.Title))
                {
                    web_library_detail.IsPurged = true;
                    WebLibraryManager.Instance.NotifyOfChangeToWebLibraryDetail();

                    MessageBoxes.Info("Your library '{0}' has been purged and will no longer be available the next time you start Qiqqa.", web_library_detail.Title);
                }
            }
            e.Handled = true;
        }

        private void HyperlinkForget_OnClick(object sender, MouseButtonEventArgs e)
        {
            WebLibraryDetail web_library_detail = DataContext as WebLibraryDetail;
            if (null != web_library_detail)
            {
                SafeThreadPool.QueueUserWorkItem(() =>
                {
                    WebLibraryManager.Instance.ForgetKnownWebLibraryFromIntranet(web_library_detail);
                });
            }
            e.Handled = true;
        }

        private void HyperlinkLocateSyncPoint_OnClick(object sender, MouseButtonEventArgs e)
        {
            WebLibraryDetail web_library_detail = DataContext as WebLibraryDetail;
            if (null != web_library_detail)
            {
                if (!String.IsNullOrEmpty(web_library_detail.IntranetPath))
                {
                    ClipboardTools.SetText(web_library_detail.IntranetPath);
                    MessageBoxes.Warn("The Intranet Library Sync Point directory name is now on your Clipboard so you can paste it into an email to your colleagues so that they can join the Intranet Library.\n\nPlease note that this Sync Point is used to synchronize your Intranet Library with others in your organisation.  Please do not modify its contents in any way.\n\n" + web_library_detail.IntranetPath);
                }
            }
            e.Handled = true;
        }

        private void HyperlinkEdit_OnClick(object sender, MouseButtonEventArgs e)
        {
            WebLibraryDetail web_library_detail = DataContext as WebLibraryDetail;
            if (null != web_library_detail)
            {
                MessageBoxes.Error("Sorry!\n\nMethod has not been implemented yet!");
            }
            e.Handled = true;
        }

        private void HyperlinkDelete_OnClick(object sender, MouseButtonEventArgs e)
        {
            WebLibraryDetail web_library_detail = DataContext as WebLibraryDetail;
            if (null != web_library_detail)
            {
                MessageBoxes.Error("Sorry!\n\nMethod has not been implemented yet!");
            }
            e.Handled = true;
        }

        private void UpdateLibraryStatistics()
        {
            WPFDoEvents.AssertThisCodeIsRunningInTheUIThread();

            UpdateLibraryStatistics_Headers();
            UpdateLibraryStatistics_Stats();
        }

        private bool have_generated_charts = false;
        private bool have_generated_cover_flow = false;

        private void UpdateLibraryStatistics_Stats()
        {
            WebLibraryDetail web_library_detail = DataContext as WebLibraryDetail;

            // Reset all
#if SYNCFUSION_ANTIQUE
            ObjCarousel.Visibility = Visibility.Collapsed;
#endif
            ObjEmptyLibraryGrid.Visibility = Visibility.Collapsed;

            if (!WebLibraryDetail.LibraryIsLoaded(web_library_detail))
            {
                return;
            }

            bool library_is_empty = (0 == web_library_detail.Xlibrary.PDFDocuments_Count);

            if (!concise_view)
            {
                // Visibility of the empty lib msg
                if (library_is_empty || ConfigurationManager.Instance.ConfigurationRecord.GUI_IsNovice)
                {
                    ObjEmptyLibraryGrid.Visibility = Visibility.Visible;
                }

                // Visibility of the graphs
                if (!library_is_empty && (ButtonCharts.IsChecked ?? false))
                {
                    if (!have_generated_charts)
                    {
                        have_generated_charts = true;
                        SafeThreadPool.QueueUserWorkItem(() =>
                        {
                            UpdateLibraryStatistics_Stats_Background_Charts(web_library_detail);
                        });
                    }
                }

                // Visibility of the coverflow
                if (!library_is_empty && (ButtonCoverFlow.IsChecked ?? false))
                {
#if SYNCFUSION_ANTIQUE
                    ObjCarousel.Visibility = Visibility.Visible;
#endif
                    if (!have_generated_cover_flow)
                    {
                        have_generated_cover_flow = true;
                        SafeThreadPool.QueueUserWorkItem(() =>
                        {
                            UpdateLibraryStatistics_Stats_Background_CoverFlow(web_library_detail);
                        });
                    }
                }
            }
        }

        private void UpdateLibraryStatistics_Stats_Background_Charts(WebLibraryDetail web_library_detail)
        {
            WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

            // The chart of the recently read and the recently added...
            const int WEEK_HISTORY = 4 * 3;
            DateTime NOW = DateTime.UtcNow;

            // Get the buckets for the past few weeks of READING
            CountingDictionary<DateTime> date_buckets_read = new CountingDictionary<DateTime>();
            if (web_library_detail.Xlibrary != null)
            {
                List<DateTime> recently_reads = web_library_detail.Xlibrary.RecentlyReadManager.GetRecentlyReadDates();
                foreach (DateTime recently_read in recently_reads)
                {
                    for (int week = 1; week < WEEK_HISTORY; ++week)
                    {
                        DateTime cutoff = NOW.AddDays(-7 * week);
                        if (recently_read >= cutoff)
                        {
                            date_buckets_read.TallyOne(cutoff);
                            break;
                        }
                    }
                }
            }

            // Get the buckets for the past few weeks of ADDING
            CountingDictionary<DateTime> date_buckets_added = new CountingDictionary<DateTime>();
            if (web_library_detail.Xlibrary != null)
            {
                foreach (PDFDocument pdf_document in web_library_detail.Xlibrary.PDFDocuments)
                {
                    for (int week = 1; week < WEEK_HISTORY; ++week)
                    {
                        DateTime cutoff = NOW.AddDays(-7 * week);
                        if (pdf_document.DateAddedToDatabase >= cutoff)
                        {
                            date_buckets_added.TallyOne(cutoff);
                            break;
                        }
                    }
                }
            }

#if SYNCFUSION_ANTIQUE
            WPFDoEvents.InvokeAsyncInUIThread(() =>
            {
                // Plot the pretty pretty
                List<ChartItem> chart_items_read = new List<ChartItem>();
                List<ChartItem> chart_items_added = new List<ChartItem>();
                for (int week = 1; week < WEEK_HISTORY; ++week)
                {
                    DateTime cutoff = NOW.AddDays(-7 * week);
                    int num_read = date_buckets_read.GetCount(cutoff);
                    int num_added = date_buckets_added.GetCount(cutoff);

                    chart_items_read.Add(new ChartItem { Title = "Read", Timestamp = cutoff, Count = num_read });
                    chart_items_added.Add(new ChartItem { Title = "Added", Timestamp = cutoff, Count = num_added });
                }

                UpdateLibraryStatistics_Stats_Background_GUI(chart_items_read, chart_items_added);
            });
#endif
        }

        private class DocumentDisplayWork
        {
            public enum StarburstColor { Blue, Pink, Green };

            public StarburstColor starburst_color;
            public string starburst_caption;
            public PDFDocument pdf_document;
            public Image image;
            public AugmentedBorder border;
            public BitmapSource page_bitmap_source;
        }

        private class DocumentDisplayWorkManager
        {
            public List<DocumentDisplayWork> ddws = new List<DocumentDisplayWork>();

            public DocumentDisplayWorkManager()
            {
            }

            public bool ContainsPDFDocument(PDFDocument pdf_document)
            {
                foreach (var ddw in ddws)
                {
                    if (ddw.pdf_document == pdf_document) return true;
                }
                return false;
            }

            public DocumentDisplayWork AddDocumentDisplayWork(DocumentDisplayWork.StarburstColor starburst_color, string starburst_caption, PDFDocument pdf_document)
            {
                DocumentDisplayWork ddw = new DocumentDisplayWork();
                ddw.starburst_color = starburst_color;
                ddw.starburst_caption = starburst_caption;
                ddw.pdf_document = pdf_document;
                ddws.Add(ddw);

                return ddw;
            }

            public int Count => ddws.Count;
        }


        private void UpdateLibraryStatistics_Stats_Background_CoverFlow(WebLibraryDetail web_library_detail)
        {
            WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

            if (web_library_detail.Xlibrary == null)
            {
                return;
            }

            List<PDFDocument> pdf_documents_all = web_library_detail.Xlibrary.PDFDocuments;

            // The list of recommended items
            DocumentDisplayWorkManager ddwm = new DocumentDisplayWorkManager();

            {
                int ITEMS_IN_LIST = 5;

                // Upcoming reading is:
                //  interrupted
                //  top priority
                //  read again
                //  recently added and no status

                pdf_documents_all.Sort(PDFDocumentListSorters.DateAddedToDatabase);

                foreach (string reading_stage in new string[] { Choices.ReadingStages_INTERRUPTED, Choices.ReadingStages_TOP_PRIORITY, Choices.ReadingStages_READ_AGAIN })
                {
                    foreach (PDFDocument pdf_document in pdf_documents_all)
                    {
                        if (!pdf_document.DocumentExists)
                        {
                            continue;
                        }

                        if (pdf_document.ReadingStage == reading_stage)
                        {
                            if (!ddwm.ContainsPDFDocument(pdf_document))
                            {
                                ddwm.AddDocumentDisplayWork(DocumentDisplayWork.StarburstColor.Pink, reading_stage, pdf_document);

                                if (ddwm.Count >= ITEMS_IN_LIST)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            {
                int ITEMS_IN_LIST = 3;

                // Recently added
                {
                    pdf_documents_all.Sort(PDFDocumentListSorters.DateAddedToDatabase);

                    int num_added = 0;
                    foreach (PDFDocument pdf_document in pdf_documents_all)
                    {
                        if (!pdf_document.DocumentExists)
                        {
                            continue;
                        }

                        if (!ddwm.ContainsPDFDocument(pdf_document))
                        {
                            ddwm.AddDocumentDisplayWork(DocumentDisplayWork.StarburstColor.Green, "Added Recently", pdf_document);

                            if (++num_added >= ITEMS_IN_LIST)
                            {
                                break;
                            }
                        }
                    }
                }

                // Recently read
                {
                    pdf_documents_all.Sort(PDFDocumentListSorters.DateLastRead);

                    int num_added = 0;
                    foreach (PDFDocument pdf_document in pdf_documents_all)
                    {
                        if (!pdf_document.DocumentExists)
                        {
                            continue;
                        }

                        if (!ddwm.ContainsPDFDocument(pdf_document))
                        {
                            ddwm.AddDocumentDisplayWork(DocumentDisplayWork.StarburstColor.Blue, "Read Recently", pdf_document);

                            if (++num_added >= ITEMS_IN_LIST)
                            {
                                break;
                            }
                        }
                    }
                }
            }

            WPFDoEvents.InvokeAsyncInUIThread(() =>
            {
                UpdateLibraryStatistics_Stats_Background_GUI_AddAllPlaceHolders(ddwm.ddws);
            });

            // fake it while we test other parts of the UI and can dearly do without the shenanigans of the PDF page rendering system:
            //
            bool allow = ConfigurationManager.IsEnabled("RenderPDFPagesForSidePanels");

            if (!allow)
            {
                WPFDoEvents.InvokeInUIThread(() =>
                {
                    foreach (DocumentDisplayWork ddw in ddwm.ddws)
                    {
                        try
                        {
                            var img = Backgrounds.GetBackground(Backgrounds.PageRenderingPending_1);
                            //ASSERT.Test(img.IsFrozen);
                            if (!img.IsFrozen)
                            {
                                img.Freeze();
                            }
                            ddw.page_bitmap_source = img;

                            UpdateLibraryStatistics_Stats_Background_GUI_FillPlaceHolder(ddw);
                        }
                        catch (Exception ex)
                        {
                            Logging.Error(ex, "UpdateLibraryStatistics_Stats_Background_CoverFlow: Error occurred.");
                            Logging.Warn(ex, "There was a problem loading a preview image for document {0}", ddw.pdf_document.Fingerprint);
                        }
                    }
                });
            }

            WPFDoEvents.InvokeAsyncInUIThread(() =>
            {
                WPFDoEvents.AssertThisCodeIsRunningInTheUIThread();

                // And fill the placeholders
                try
                {
                    // fake it while we test other parts of the UI and can dearly do without the shenanigans of the PDF page rendering system:
                    //
                    bool allow = ConfigurationManager.IsEnabled("RenderPDFPagesForSidePanels");

                    if (!allow)
                    {
                        foreach (DocumentDisplayWork ddw in ddwm.ddws)
                        {
                            try
                            {
                                var img = Backgrounds.GetBackground(Backgrounds.PageRenderingPending_2);
                                //ASSERT.Test(img.IsFrozen);
                                if (!img.IsFrozen)
                                {
                                    img.Freeze();
                                }
                                ddw.page_bitmap_source = img;

                                UpdateLibraryStatistics_Stats_Background_GUI_FillPlaceHolder(ddw);
                            }
                            catch (Exception ex)
                            {
                                Logging.Error(ex, "UpdateLibraryStatistics_Stats_Background_CoverFlow: Error occurred.");
                                Logging.Warn(ex, "There was a problem loading a preview image for document {0}", ddw.pdf_document.Fingerprint);
                            }
                        }
                    }

                    SafeThreadPool.QueueUserWorkItem(() =>
                    {
                        try
                        {
                            // Now render each document
                            using (Font font = new Font("Times New Roman", 9.0f))
                            {
                                using (StringFormat string_format = new StringFormat
                                {
                                    Alignment = StringAlignment.Center,
                                    LineAlignment = StringAlignment.Center
                                })
                                {
                                    var color_matrix = new ColorMatrix();
                                    color_matrix.Matrix33 = 0.9f;
                                    using (var image_attributes = new ImageAttributes())
                                    {
                                        image_attributes.SetColorMatrix(color_matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                                        foreach (DocumentDisplayWork ddw in ddwm.ddws)
                                        {
                                            try
                                            {
                                                using (MemoryStream ms = new MemoryStream(ddw.pdf_document.GetPageByHeightAsImage(1, (int)Math.Round(PREVIEW_IMAGE_HEIGHT / PREVIEW_IMAGE_PERCENTAGE), (int)Math.Round(PREVIEW_IMAGE_WIDTH / PREVIEW_IMAGE_PERCENTAGE))))
                                                {
                                                    using (Bitmap page_bitmap_ms = (Bitmap)System.Drawing.Image.FromStream(ms))
                                                    {
                                                        using (Bitmap page_bitmap = page_bitmap_ms.Clone(new RectangleF { Width = page_bitmap_ms.Width, Height = (int)Math.Round(page_bitmap_ms.Height * PREVIEW_IMAGE_PERCENTAGE) }, page_bitmap_ms.PixelFormat))
                                                        {
                                                            using (Graphics g = Graphics.FromImage(page_bitmap))
                                                            {
                                                                int CENTER = 60;
                                                                int RADIUS = 60;

                                                                {
                                                                    BitmapImage starburst_bi = null;
                                                                    switch (ddw.starburst_color)
                                                                    {
                                                                        case DocumentDisplayWork.StarburstColor.Blue:
                                                                            starburst_bi = Icons.GetAppIcon(Icons.PageCornerBlue);
                                                                            break;
                                                                        case DocumentDisplayWork.StarburstColor.Green:
                                                                            starburst_bi = Icons.GetAppIcon(Icons.PageCornerGreen);
                                                                            break;
                                                                        case DocumentDisplayWork.StarburstColor.Pink:
                                                                            starburst_bi = Icons.GetAppIcon(Icons.PageCornerPink);
                                                                            break;
                                                                        default:
                                                                            starburst_bi = Icons.GetAppIcon(Icons.PageCornerOrange);
                                                                            break;
                                                                    }

                                                                    using (Bitmap starburst_image = BitmapImageTools.ConvertBitmapSourceToBitmap(starburst_bi))
                                                                    {
                                                                        g.SmoothingMode = SmoothingMode.AntiAlias;
                                                                        g.DrawImage(
                                                                            starburst_image,
                                                                            new Rectangle(CENTER - RADIUS, CENTER - RADIUS, 2 * RADIUS, 2 * RADIUS),
                                                                            0,
                                                                            0,
                                                                            starburst_image.Width,
                                                                            starburst_image.Height,
                                                                            GraphicsUnit.Pixel,
                                                                            image_attributes
                                                                        );
                                                                    }
                                                                }

                                                                using (Matrix mat = new Matrix())
                                                                {
                                                                    mat.RotateAt(-50, new PointF(CENTER / 2, CENTER / 2));
                                                                    g.Transform = mat;

                                                                    string wrapped_caption = ddw.starburst_caption;
                                                                    wrapped_caption = wrapped_caption.ToLower();
                                                                    wrapped_caption = Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(wrapped_caption);
                                                                    wrapped_caption = wrapped_caption.Replace(" ", "\n");
                                                                    g.DrawString(wrapped_caption, font, Brushes.Black, new PointF(CENTER / 2, CENTER / 2), string_format);
                                                                }
                                                            }

                                                            BitmapSource page_bitmap_source = BitmapImageTools.CreateBitmapSourceFromImage(page_bitmap);
                                                            ASSERT.Test(page_bitmap_source.IsFrozen);

                                                            ddw.page_bitmap_source = page_bitmap_source;
                                                        }
                                                    }
                                                }

#if false  // do this bit further below, all at once for all entries, in the UI thread!
                                                try
                                                {
                                                    UpdateLibraryStatistics_Stats_Background_GUI_FillPlaceHolder(ddw);
                                                }
                                                catch (Exception ex)
                                                {
                                                    Logging.Error(ex, "UpdateLibraryStatistics_Stats_Background_CoverFlow: Error occurred.");
                                                    throw;
                                                }
#endif
                                            }
                                            catch (Exception ex)
                                            {
                                                Logging.Warn(ex, "There was a problem loading a preview image for document {0}", ddw.pdf_document.Fingerprint);

                                                Logging.Error(ex, "UpdateLibraryStatistics_Stats_Background_CoverFlow: Error occurred.");

                                                // do not rethrow the error: allow the other pages in the pages to be rendered...

                                                ddw.page_bitmap_source = Backgrounds.GetBackground(Backgrounds.PageRenderingFailed_ClassicNews);
                                            }

                                            // https://stackoverflow.com/questions/9732709/the-calling-thread-cannot-access-this-object-because-a-different-thread-owns-it#answer-33917169
                                            ddw.page_bitmap_source.Freeze();
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logging.Error(ex, "UpdateLibraryStatistics_Stats_Background_CoverFlow: Error occurred.");
                        }

                        // Don't care if there were errors in the process so far: the pages which got rendered, SHOULD make it into the UI anyway!
                        WPFDoEvents.InvokeAsyncInUIThread(() =>
                        {
                            foreach (DocumentDisplayWork ddw in ddwm.ddws)
                            {
                                try
                                {
                                    UpdateLibraryStatistics_Stats_Background_GUI_FillPlaceHolder(ddw);
                                }
                                catch (Exception ex)
                                {
                                    Logging.Error(ex, "UpdateLibraryStatistics_Stats_Background_CoverFlow: Error occurred.");
                                    Logging.Warn(ex, "There was a problem loading a preview image for document {0}", ddw.pdf_document.Fingerprint);
                                }
                            }

                            if (0 == ddwm.ddws.Count)
                            {
                                ButtonCoverFlow.IsChecked = false;
                                UpdateLibraryStatistics();
                            }
                        });
                    });
                }
                catch (Exception ex)
                {
                    Logging.Error(ex, "UpdateLibraryStatistics_Stats_Background_CoverFlow: Error occurred.");
                }

                if (0 == ddwm.ddws.Count)
                {
                    ButtonCoverFlow.IsChecked = false;
                    UpdateLibraryStatistics();
                }
            });
        }

        private void UpdateLibraryStatistics_Stats_Background_GUI_AddAllPlaceHolders(List<DocumentDisplayWork> ddws)
        {
            WPFDoEvents.AssertThisCodeIsRunningInTheUIThread();

#if SYNCFUSION_ANTIQUE
            ObjCarousel.Items.Clear();

            foreach (DocumentDisplayWork ddw in ddws)
            {
                Image image = new Image();
                image.Tag = ddw;
                image.Stretch = Stretch.Uniform;
                image.Width = 600;

                AugmentedBorder border = new AugmentedBorder();
                border.Tag = ddw;
                border.Child = image;
                border.Visibility = Visibility.Collapsed;

                ASSERT.Test(image.Source == null);
                ddw.image = image;
                ddw.border = border;

                ObjCarousel.Items.Add(border);
            }

            if (0 < ObjCarousel.Items.Count)
            {
                ObjCarousel.SelectedIndex = 0;
            }
#endif
        }

        private void UpdateLibraryStatistics_Stats_Background_GUI_FillPlaceHolder(DocumentDisplayWork ddw)
        {
            WPFDoEvents.AssertThisCodeIsRunningInTheUIThread();

            if (ddw.image != null)
            {
                ddw.image.Source = ddw.page_bitmap_source ?? Backgrounds.GetBackground(Backgrounds.PageRenderingFailed_Relax);
                ddw.image.Stretch = Stretch.Uniform;
                ASSERT.Test(ddw.image.Source.IsFrozen);

                ddw.border.Visibility = Visibility.Visible;
            }
        }

        private void ObjCarousel_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            WPFDoEvents.AssertThisCodeIsRunningInTheUIThread();

#if SYNCFUSION_ANTIQUE
            if (0 < ObjCarousel.Items.Count && 0 <= ObjCarousel.SelectedIndex)
            {
                FrameworkElement fe = null;

                // First try the selected value
                if (null == fe)
                {
                    fe = (FrameworkElement)ObjCarousel.SelectedValue;
                }

                // If that doesn't work, then try any item in the carousel
                if (null == fe)
                {
                    if (0 < ObjCarousel.Items.Count)
                    {
                        fe = (FrameworkElement)ObjCarousel.Items[0];
                    }
                }

                if (null != fe)
                {
                    DocumentDisplayWork ddw = (DocumentDisplayWork)fe.Tag;
                    MainWindowServiceDispatcher.Instance.OpenDocument(ddw.pdf_document);
                }
            }
#endif

            e.Handled = true;
        }

#if SYNCFUSION_ANTIQUE

        private void UpdateLibraryStatistics_Stats_Background_GUI(List<ChartItem> chart_items_read, List<ChartItem> chart_items_added)
        {
            WPFDoEvents.AssertThisCodeIsRunningInTheUIThread();
        }

        private void UpdateLibraryStatistics_Headers()
        {
            WPFDoEvents.AssertThisCodeIsRunningInTheUIThread();

            TextLibraryCount.Text = "";

            PanelForHyperlinks.Visibility = Visibility.Visible;
            PanelForget.Visibility = Visibility.Collapsed;
            PanelSetSyncPoint.Visibility = Visibility.Collapsed;
            PanelLocateSyncPoint.Visibility = Visibility.Collapsed;
            PanelEdit.Visibility = Visibility.Collapsed;
            PanelDelete.Visibility = Visibility.Collapsed;
            PanelPurge.Visibility = Visibility.Collapsed;

            ButtonAutoSync.Visibility = Visibility.Collapsed;
            ButtonReadOnly.Visibility = Visibility.Collapsed;

            ObjTitleImage.Source = null;

            // Store the web library details
            WebLibraryDetail web_library_detail = DataContext as WebLibraryDetail;
            // No need to wait until the library has been completely loaded!
            drag_to_library_manager.DefaultLibrary = web_library_detail;

            if (null != web_library_detail)
            {
                if (!web_library_detail.IsIntranetLibrary)
                {
                    TextLibraryCount.Text = String.Format("{0} document(s) in this library", web_library_detail.Xlibrary?.PDFDocuments_Count ?? 0);
                }
                else
                {
                    TextLibraryCount.Text = String.Format("{0} document(s) in this library, {1}",
                        web_library_detail.Xlibrary?.PDFDocuments_Count ?? 0,
                        web_library_detail.LastSynced.HasValue ? $"which was last synced on {web_library_detail.LastSynced.Value}" : @"which has never been synced yet");
                }

                // The wizard stuff
                if (ConfigurationManager.Instance.ConfigurationRecord.GUI_IsNovice)
                {
                    WizardDPs.SetPointOfInterest(ButtonIcon, "GuestLibraryOpenButton");
                    WizardDPs.SetPointOfInterest(TxtTitle, "GuestLibraryTitle");
                }

                // The icon stuff
                {
                    //RenderOptions.SetBitmapScalingMode(ButtonIcon, BitmapScalingMode.HighQuality);
                    ButtonIcon.Width = 64;
                    ButtonIcon.Height = 64;

                    if (web_library_detail.IsIntranetLibrary)
                    {
                        ButtonIcon.Source = Icons.GetAppIcon(Icons.LibraryTypeWeb);
                        //ButtonIcon.Source = Icons.GetAppIcon(Icons.LibraryTypeIntranet);
                        ButtonIcon.ToolTip = "This is an Intranet Library.\nYou can sync it via your Intranet to share with colleagues and across your company computers. Alternatively you can sync to a folder in Cloud Storage such as DropBox, Google Drive or Microsoft OneDrive and anyone with access to that shared folder can sync with your library.";
                    }
                    else if (web_library_detail.IsBundleLibrary)
                    {
                        ButtonIcon.Source = Icons.GetAppIcon(Icons.LibraryTypeBundle);
                        ButtonIcon.ToolTip = "This is a Bundle Library.\nIt's contents will be updated automatically when the Bundle is updated by it's administrator.";
                    }
                    else
                    {
                        ButtonIcon.Source = Icons.GetAppIcon(Icons.LibraryTypeGuest);
                        ButtonIcon.ToolTip = "This is a local Library.\nIts contents are local to this computer. When you assign this library a Sync Point, it can be synchronized with that backup location and possibly shared with other people and machines.";
                    }
                }

                // The customization images stuff
                {
                    string image_filename = CustomBackgroundFilename();
                    if (File.Exists(image_filename))
                    {
                        try
                        {
                            ObjTitleImage.Source = BitmapImageTools.FromImage(ImageLoader.Load(image_filename));
                            ASSERT.Test(ObjTitleImage.Source.IsFrozen);
                        }
                        catch (Exception ex)
                        {
                            Logging.Warn(ex, "Problem with custom library background.");
                        }
                    }
                }

                {
                    string image_filename = CustomIconFilename();
                    if (File.Exists(image_filename))
                    {
                        try
                        {
                            ButtonIcon.Source = BitmapImageTools.FromImage(ImageLoader.Load(image_filename));
                        }
                        catch (Exception ex)
                        {
                            Logging.Warn(ex, "Problem with custom library icon.");
                        }
                    }
                }

                // The autosync stuff
                if (web_library_detail.IsIntranetLibrary)
                {
                    ButtonAutoSync.Visibility = Visibility.Visible;
                    ButtonAutoSync.IsChecked = web_library_detail.AutoSync;
                }

                // The readonly stuff
                if (web_library_detail.IsReadOnlyLibrary)
                {
                    ButtonReadOnly.Visibility = Visibility.Visible;
                }

                // The hyperlinks panel
                PanelEdit.Visibility = Visibility.Visible;
                PanelDelete.Visibility = Visibility.Visible;
                PanelForget.Visibility = Visibility.Visible;
                //PanelSetSyncPoint.Visibility = Visibility.Visible;
                //PanelLocateSyncPoint.Visibility = Visibility.Visible;
                PanelEdit.Visibility = Visibility.Visible;
                PanelDelete.Visibility = Visibility.Visible;
                //PanelPurge.Visibility = Visibility.Visible;

                if (web_library_detail.Deleted)
                {
                    PanelPurge.Visibility = Visibility.Visible;
                }

                if (web_library_detail.IsIntranetLibrary)
                {
                    PanelLocateSyncPoint.Visibility = Visibility.Visible;
                }

                if (!web_library_detail.IsReadOnlyLibrary)
                {
                    PanelSetSyncPoint.Visibility = Visibility.Visible;
                }
            }
        }

        private string CustomIconFilename()
        {
            WebLibraryDetail web_library_detail = DataContext as WebLibraryDetail;

            return Path.GetFullPath(Path.Combine(web_library_detail.LIBRARY_BASE_PATH ?? ConfigurationManager.Instance.BaseDirectoryForQiqqa, @"Qiqqa.library_custom_icon.png"));
        }

        private string CustomBackgroundFilename()
        {
            WebLibraryDetail web_library_detail = DataContext as WebLibraryDetail;

            return Path.GetFullPath(Path.Combine(web_library_detail.LIBRARY_BASE_PATH ?? ConfigurationManager.Instance.BaseDirectoryForQiqqa, @"Qiqqa.library_custom_background.jpg"));
        }

        internal void CustomiseBackground()
        {
            GenericCustomiseChooser(
                "Please select an image to use as the background of this Library.  Press CANCEL to remove any existing background.",
                CustomBackgroundFilename()
            );
        }

        internal void CustomiseIcon()
        {
            GenericCustomiseChooser(
                "Please select an image to use as the icon of this Library.  Press CANCEL to remove any existing icon.",
                CustomIconFilename()
            );
        }

        private void GenericCustomiseChooser(string title, string filename)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Image files|*.jpeg;*.jpg;*.png;*.gif;*.bmp" + "|" + "All files|*.*";
            dialog.CheckFileExists = true;
            dialog.Multiselect = false;
            dialog.Title = title;
            //dialog.FileName = filename;
            if (true == dialog.ShowDialog())
            {
                // Copy the new file into place, if it is another file than the one we already have:
                filename = Path.GetFullPath(filename);
                string new_filename = Path.GetFullPath(dialog.FileName);
                if (0 != new_filename.CompareTo(filename))
                {
                    File.Delete(filename);
                    File.Copy(new_filename, filename);
                }
            }
            else
            {
                File.Delete(filename);
            }

            UpdateLibraryStatistics();
        }
    }

#if SYNCFUSION_ANTIQUE

    public class ChartItem
    {
        public string Title { get; set; }
        public DateTime Timestamp { get; set; }
        public int Count { get; set; }
    }

    public class ToolTipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (null == value)
            {
                return "";
            }

            ChartSegment chart_segment = value as ChartSegment;
            IList data_source = (IList)chart_segment.Series.DataSource;
            ChartItem chart_item = (ChartItem)data_source[chart_segment.CorrespondingPoints[0].Index];
            return String.Format("During the week starting {0} you {1} {2} paper(s)", DateFormatter.asDDMMMYYYY(chart_item.Timestamp), chart_segment.Series.Name.ToLower(), chart_item.Count);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

#endif

}

