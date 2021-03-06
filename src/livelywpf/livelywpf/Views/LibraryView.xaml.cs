﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace livelywpf.Views
{
    /// <summary>
    /// Interaction logic for LibraryView.xaml
    /// </summary>
    public partial class LibraryView : System.Windows.Controls.Page
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        livelygrid.LivelyGridView LivelyGridControl { get; set; }

        public LibraryView()
        {
            InitializeComponent();
            //uwp control also gets binded..
            this.DataContext = Program.LibraryVM; 
        }

        private void LivelyGridView_ChildChanged(object sender, EventArgs e)
        {
            // Hook up x:Bind source.
            global::Microsoft.Toolkit.Wpf.UI.XamlHost.WindowsXamlHost windowsXamlHost =
                sender as global::Microsoft.Toolkit.Wpf.UI.XamlHost.WindowsXamlHost;
            LivelyGridControl = windowsXamlHost.GetUwpInternalObject() as global::livelygrid.LivelyGridView;

            if (LivelyGridControl != null)
            {
                //Don't know if there is an easier way to chang UserControl language, tried setting framework language to no effect.
                //todo: find better way to do this.
                LivelyGridControl.UIText = new livelygrid.LocalizeText()
                {
                    TextAddWallpaper = Properties.Resources.TitleAddWallpaper,
                    TextConvertVideo = Properties.Resources.TextConvertVideo,
                    TextCustomise = Properties.Resources.TextCustomiseWallpaper,
                    TextDelete = Properties.Resources.TextDeleteWallpaper,
                    TextExportZip = Properties.Resources.TextExportWallpaperZip,
                    TextInformation = Properties.Resources.TitleAbout,
                    TextSetWallpaper = Properties.Resources.TextSetWallpaper,
                    TextShowDisk = Properties.Resources.TextShowOnDisk
                };
                LivelyGridControl.GridElementSize((livelygrid.GridSize)Program.SettingsVM.SelectedTileSizeIndex);
                LivelyGridControl.ContextMenuClick += LivelyGridControl_ContextMenuClick;
                LivelyGridControl.FileDroppedEvent += LivelyGridControl_FileDroppedEvent;
            }
        }

        /// <summary>
        /// Not possible to do direct mvvm currently, putting the contextmenu inside datatemplate works but.. 
        /// the menu is opening only when right clicking on the DataTemplate content which is not covering completely the GridViewItem.
        /// So the workaround I did is set it outside of template and the datacontext is calculated in code behind.
        /// ref: https://github.com/microsoft/microsoft-ui-xaml/issues/911
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void LivelyGridControl_ContextMenuClick(object sender, object e)
        {
            var s = sender as MenuFlyoutItem;
            LibraryModel obj;
            try
            {
                obj = (LibraryModel)e;
            }
            catch { return; }

            switch (s.Name)
            {
                case "showOnDisk":
                    Program.LibraryVM.WallpaperShowOnDisk(e);
                    break;
                case "setWallpaper":
                    Program.LibraryVM.WallpaperSet(e);
                    break;
                case "exportWallpaper":
                    string savePath = "";
                    var saveFileDialog1 = new Microsoft.Win32.SaveFileDialog()
                    {
                        Title = "Select location to save the file",
                        Filter = "Lively/zip file|*.zip",
                        FileName = ((LibraryModel)e).Title,
                    };
                    if (saveFileDialog1.ShowDialog() == true)
                    {
                        savePath = saveFileDialog1.FileName;
                    }
                    if (String.IsNullOrEmpty(savePath))
                    {
                        break;
                    }
                    Program.LibraryVM.WallpaperExport(e, savePath);
                    break;
                case "deleteWallpaper":
                    var result = await ShowDeleteConfirmationDialog(sender, e);
                    if (result == ContentDialogResult.Primary)
                    {
                        Program.LibraryVM.WallpaperDelete(e);
                    }
                    break;
                case "customiseWallpaper":                    
                    //In app customise dialogue; 
                    //Can't use contentdialogue since the window object is not uwp.
                    //modernwpf contentdialogue does not have xamlroot so can't draw over livelygrid.
                    LivelyGridControl.DimBackground(true);
                    var overlay = new Cef.LivelyPropertiesWindow(obj, Program.LibraryVM.GetLivelyPropertyCopyPath(e)) {
                        Owner = App.AppWindow, 
                        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                        Width = App.AppWindow.Width/1.5,
                        Height = App.AppWindow.Height/1.5,
                        Title = obj.Title + " Properties"
                    };
                    overlay.ShowDialog();
                    LivelyGridControl.DimBackground(false);                
                    break;
                case "convertVideo":
                    Program.LibraryVM.WallpaperVideoConvert(obj);
                    break;
                case "moreInformation":
                    throw new NotImplementedException();
                    break;
            }
        }

        private async void LivelyGridControl_FileDroppedEvent(object sender, Windows.UI.Xaml.DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.WebLink))
            {
                var uri = await e.DataView.GetWebLinkAsync();
                Logger.Info("Dropped url:- " + uri.ToString());
                if (libVLCStreams.CheckStream(uri))
                {
                    Program.LibraryVM.AddWallpaper(uri.ToString(), WallpaperType.videostream, LibraryTileType.processing, ScreenHelper.GetPrimaryScreen());
                }
                else
                {
                    Program.LibraryVM.AddWallpaper(uri.ToString(), WallpaperType.url, LibraryTileType.processing, ScreenHelper.GetPrimaryScreen());
                }
            }
            else if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    //selecting first file only.
                    var item = items[0].Path;
                    Logger.Info("Dropped file:- " + item);
                    try
                    {
                        if (String.IsNullOrWhiteSpace(Path.GetExtension(item)))
                            return;
                    }
                    catch (ArgumentException)
                    {
                        Logger.Info("Invalid character, skipping dropped file:- " + item);
                        return;
                    }

                    if (Path.GetExtension(item).Equals(".gif", StringComparison.OrdinalIgnoreCase))
                    {
                        Program.LibraryVM.AddWallpaper(item, WallpaperType.gif, LibraryTileType.processing, ScreenHelper.GetPrimaryScreen());
                    }
                    else if (Path.GetExtension(item).Equals(".html", StringComparison.OrdinalIgnoreCase))
                    {
                        Program.LibraryVM.AddWallpaper(item, WallpaperType.web, LibraryTileType.processing, ScreenHelper.GetPrimaryScreen());
                    }
                    else if (Path.GetExtension(item).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        Program.LibraryVM.WallpaperInstall(item);
                    }
                    else if (FileOperations.IsVideoFile(item))
                    {
                        Program.LibraryVM.AddWallpaper(item, WallpaperType.video, LibraryTileType.processing, ScreenHelper.GetPrimaryScreen());
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("not supported currently");
                    }
                }
            }
        }

        //todo: make dialogue service.
        private async Task<ContentDialogResult> ShowDeleteConfirmationDialog(object sender, object arg)
        {
            var item = (MenuFlyoutItem)sender;
            var lib = (LibraryModel)arg;

            var tb = new Windows.UI.Xaml.Controls.TextBlock
            {
                Text = lib.Title + " by " + lib.LivelyInfo.Author
            };
            ContentDialog deleteDialog = new ContentDialog
            {
                Title = Properties.Resources.DescriptionDeleteConfirmation,
                Content = tb,
                PrimaryButtonText = Properties.Resources.TextYes,
                SecondaryButtonText = Properties.Resources.TextNo
            };

            // Use this code to associate the dialog to the appropriate AppWindow by setting
            // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                deleteDialog.XamlRoot = item.XamlRoot;
            }

            ContentDialogResult result = await deleteDialog.ShowAsync();
            return result;
        }

        private void Page_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (LivelyGridControl != null)
            {
                LivelyGridControl.ContextMenuClick -= LivelyGridControl_ContextMenuClick;
                LivelyGridControl.FileDroppedEvent -= LivelyGridControl_FileDroppedEvent;
                //stop rendering previews... this should be automatic(?), but its not for some reason.
                LivelyGridControl.GridElementSize(livelygrid.GridSize.NoPreview);
            }
        }
    }
}
