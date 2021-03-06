﻿using Osmo.Core;
using Osmo.Core.Logging;
using Osmo.Core.Objects;
using Osmo.Core.Reader;
using Osmo.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Osmo.UI
{
    /// <summary>
    /// Interaction logic for ResizeTool.xaml
    /// </summary>
    public partial class ResizeTool : Grid, ISkinContainer
    {
        private static ResizeTool _instance;

        public static ResizeTool Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ResizeTool();
                }

                return _instance;
            }
        }
        public Skin LoadedSkin
        {
            get => (DataContext as ResizeToolViewModel).SelectedSkin;
        }

        private ResizeTool()
        {
            InitializeComponent();
        }

        public void LoadSkin(Skin skin)
        {
            ResizeToolViewModel vm = DataContext as ResizeToolViewModel;
            vm.SelectedSkin = skin;
            Logger.Instance.WriteLog("Skin \"{0}\" loaded in Resize Tool!", skin.Name);
        }

        public void UnloadSkin(Skin skin)
        {
            ResizeToolViewModel vm = DataContext as ResizeToolViewModel;
            if (vm.SelectedSkin?.Equals(skin) ?? false)
            {
                vm.SelectedSkin = null;
            }
        }

        private void Rezize_Click(object sender, RoutedEventArgs e)
        {
            ResizeToolViewModel vm = DataContext as ResizeToolViewModel;
            bool keepOriginalFiles = vm.FileOption_keepOriginal;
            bool optimalSize = vm.ResizeOption_OptimalSize;
            string version = vm.SelectedSkin.Version;
            bool freezeThread = false;

            Logger.Instance.WriteLog("Initializing resize operation...\n Selected options: " +
                "\n\tSkin: {0}\n\tSkin version: {1}\n\tKeep original files: {2}\n\tUse recommended size: {3}",
                vm.SelectedSkin.Name, version, keepOriginalFiles, optimalSize);

            vm.ElementsResizeValue = 0;
            vm.IsResizing = true;
            new Thread(() =>
            {
                List<SkinElement> elements = vm.SelectedSkin.Elements.Where(x => x.IsResizeSelected).ToList();
                vm.ElementsResizeMaximum = elements.Count;
                Logger.Instance.WriteLog("Number of selected elements: {0}", elements.Count);

                foreach (SkinElement item in elements)
                {
                    vm.CurrentFile = item.Name;
                    vm.ElementsResizeValue++;
                    string newPath = null;

                    if (keepOriginalFiles)
                    {
                        if (item.IsHighDefinition)
                        {
                            newPath = item.Path.Replace("@2x", "");
                        }
                        else
                        {
                            newPath = item.Path.Replace(item.Name, item.Name + "@2x");
                        }
                    }
                    else
                    {
                        newPath = item.Path;
                    }

                    TransformedBitmap source = ResizeImage(item, optimalSize, version, out bool isDistorted);
                    bool saveFile = true;

                    if (isDistorted)
                    {
                        App.Current.Dispatcher.Invoke(async () =>
                        {
                            freezeThread = true;
                            var msgBox = MaterialMessageBox.Show(Helper.FindString("dlg_distortedImageTitle"),
                                string.Format("{0}{1}{2}", Helper.FindString("dlg_distortedImageDescription1"), item.Name, Helper.FindString("dlg_distortedImageDescription2")),
                                Helper.FindString("dlg_distortedImageUseHalveSize"),
                                Helper.FindString("dlg_distortedImageProceed"),
                                Helper.FindString("dlg_distortedImageSkip"), 450);

                            await DialogHelper.Instance.ShowDialog(msgBox);

                            if (msgBox.Result == OsmoMessageBoxResult.CustomActionLeft) //Skip the current element
                            {
                                saveFile = false;
                            }
                            else if (msgBox.Result == OsmoMessageBoxResult.CustomActionRight) //Resize the image again but halve image size
                            {
                                source = ResizeImage(item, false, version, out isDistorted);
                            }

                            freezeThread = false;
                        });

                        while (freezeThread) //Wait for the dialog to be closed
                        {
                            Thread.Sleep(100);
                        }
                    }

                    if (saveFile)
                    {
                        if (item.Extension.Contains("png"))
                        {
                            TransformedBitmapToFile<PngBitmapEncoder>(newPath, source);
                        }
                        else
                        {
                            TransformedBitmapToFile<JpegBitmapEncoder>(newPath, source);
                        }
                    }
                }

                Logger.Instance.WriteLog("Resizing done!");
                vm.IsResizing = false;
            }).Start();
        }

        private TransformedBitmap ResizeImage(SkinElement element, bool resize_optimalSize, string skinVersion,
            out bool isDistorted)
        {
            try { 
            BitmapSource image = Helper.LoadImage(element.Path);
            Size targetSize;
            SkinningEntry entry = element.ElementDetails as SkinningEntry;

            if (resize_optimalSize && entry != null && 
                entry.SuggestedSDSize != null && !entry.SuggestedSDSize.Equals(new Size()))
            {
                targetSize = entry.GetSuggestedSizeForVersion(skinVersion);
            }
            else
            {
                targetSize = new Size(Math.Round(element.ImageSize.Width / 2), Math.Round(element.ImageSize.Height / 2));
            }

            double scaleX = targetSize.Width / image.PixelWidth,
                scaleY = targetSize.Height / image.PixelHeight;

            double distortionFactor = scaleX - scaleY;

            isDistorted = distortionFactor > 0.2 || distortionFactor < -0.2;

            return new TransformedBitmap(image,
                new ScaleTransform(
                    scaleX,
                    scaleY));
            }
            catch (Exception ex)
            {
                Logger.Instance.WriteLog("Error while resizing element \"{0}\"!", LogType.ERROR, ex, element.Name);
                isDistorted = false;
                return null;
            }
        }

        private bool TransformedBitmapToFile<T>(string targetPath, BitmapSource source) where T : BitmapEncoder, new()
        {
            if (string.IsNullOrWhiteSpace(targetPath) || source == null)
            {
                return false;
            }

            var frame = BitmapFrame.Create(source);
            var encoder = new T();
            encoder.Frames.Add(frame);
            try
            {
                using (var fs = new FileStream(targetPath, FileMode.Create))
                {
                    encoder.Save(fs);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.WriteLog("Failed to save resized image! (Target path: {0})", LogType.ERROR, ex, targetPath);
                return false;
            }

            return true;
        }
    }
}
