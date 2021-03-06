﻿using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Uninstaller.Objects;
using System.Threading;

namespace Uninstaller.UI
{
    /// <summary>
    /// Interaktionslogik für Uninstall.xaml
    /// </summary>
    public partial class Uninstall : UserControl, IManagedUI
    {
        MainWindow window;
        string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + App.AppName + "\\",
            desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        Logger logger = new Logger();
        bool keepData;
        BackgroundWorker setup = new BackgroundWorker();

        public Uninstall()
        {
            InitializeComponent();
            setup.DoWork += Setup_DoWork;
            setup.RunWorkerCompleted += Setup_RunWorkerCompleted;
        }

        private void Setup_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            progress.Value = progress.Maximum;
            txt_status.Text = GlobalValues.AppName + " uninstalled!";
            txt_log.Text += "\n\n" + GlobalValues.AppName + " uninstalled!";
            window.btn_next.IsEnabled = true;
        }

        private void Setup_DoWork(object sender, DoWorkEventArgs e)
        {
            KillProcess(GlobalValues.AppName);
            RemoveFiles();
            RemoveFromRegistry();
        }

        private void RemoveFiles()
        {
            DirectoryInfo root = new DirectoryInfo(GlobalValues.InstallationPath);
            int files = root.GetDirectories().Length + root.GetFiles().Length;

            Invoker.InvokeProgress(progress, 0, files);
            foreach (DirectoryInfo di in root.GetDirectories())
            {
                if (!KeepData(di.Name))
                {
                    Directory.Delete(di.FullName, true);
                    PrintMessage(di.Name, true);
                }
            }

            foreach (FileInfo fi in root.EnumerateFiles())
            {
                if (!KeepData(fi.Name))
                {
                    File.Delete(fi.FullName);
                    PrintMessage(fi.Name, false);
                }
            }

            Helper.DeleteFile(string.Format("{0}\\" + GlobalValues.AppName + ".lnk", desktop));
            Helper.DeleteDirectory(string.Format("{0}\\" + GlobalValues.AppName,
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)), false);
        }

        private void PrintMessage(string objName, bool isFolder)
        {
            string message;
            if (isFolder)
                message = string.Format("Deleting folder \"{0}\"", objName);
            else
                message = string.Format("Deleting file \"{0}\"", objName);
            Invoker.InvokeStatus(progress, txt_log, txt_status, message);
        }

        private bool KeepData(string name)
        {
            if (keepData)
            {
                if (name.Contains(".cfg") || name.Contains(".sqlite") ||
                    name.Equals("Profiles") || name.Equals("Backups") || name.Equals("Templates") ||
                    name.Equals("Logs"))
                    return true;
                else
                    return false;
            }
            else
                return false;
        }

        private void RemoveFromRegistry()
        {
            RegistryKey edgeKey = Registry.CurrentUser.OpenSubKey(@"Software\" + GlobalValues.AppName, false);
            string guidPath = edgeKey.GetValue("GUID").ToString();
            edgeKey.Close();
            Registry.CurrentUser.DeleteSubKey(@"Software\" + GlobalValues.AppName);
            Registry.CurrentUser.DeleteSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + guidPath);
        }

        private void KillProcess(string processName)
        {
            bool isRunning = Process.GetProcessesByName(processName).Length > 0;
            if (isRunning)
            {
                Process[] MatchingProcesses = Process.GetProcessesByName(processName);
                foreach (Process p in MatchingProcesses)
                    p.Kill();
            }
            Thread.Sleep(500);
        }

        public void RegisterParent(MainWindow window)
        {
            this.window = window;
            window.btn_cancel.IsEnabled = false;
            keepData = MessageBox.Show("Do you want to keep your personal data? (Profiles, settings, pending skin changes)", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
            setup.RunWorkerAsync();
        }
    }
}
