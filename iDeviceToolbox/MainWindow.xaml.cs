using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

using iMobileDevice;
using iMobileDevice.Afc;
using iMobileDevice.iDevice;
using iMobileDevice.Lockdown;
using iMobileDevice.Plist;
using iMobileDevice.Restore;
using iMobileDevice.Recovery;
using System.Net;

namespace iDeviceToolbox
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private iDeviceHandle deviceHandle;
        private LockdownClientHandle lockdownClientHandle;
        private IiDeviceApi iDevice = LibiMobileDevice.Instance.iDevice;
        private ILockdownApi lockdown = LibiMobileDevice.Instance.Lockdown;
        private IRecoveryApi recoveryDevice = LibiMobileDevice.Instance.Recovery;
        private RecoveryClientHandle recoveryClientHandle;
        private bool isconnected = false;
        private string jailbreak = "None";
        public MainWindow()
        {
            InitializeComponent();
        }
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // set the tab control to invisible and start the device detection
            dashboard.Visibility = Visibility.Hidden;
            NativeLibraries.Load();
            await Task.Run(() => DeviceDetection());
        }
        private void DeviceDetection()
        {
            while (!isconnected) {
                int count = 0;
                // get the device list
                iDeviceError error = iDevice.idevice_get_device_list(out ReadOnlyCollection<string> deviceList, ref count);
                // if deviceList is null, then there is no device connected
                if (count == 0)
                {
                    isconnected = false;
                    // check for device in recovery mode
                    // we can do this by using irecovery.exe -q
                    using (Process p = new Process())
                    {
                        p.StartInfo.FileName = AppDomain.CurrentDomain.BaseDirectory + "win-x64\\irecovery.exe";
                        p.StartInfo.Arguments = "-q";
                        p.StartInfo.CreateNoWindow = true;
                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.RedirectStandardOutput = true;
                        p.Start();
                        while (!p.StandardOutput.EndOfStream)
                        {
                            string line = p.StandardOutput.ReadLine();
                            if (line.StartsWith("ECID:"))
                            {
                                string recoveryEcid = line.Remove(0, 5).Trim().TrimStart('0').TrimStart('x').TrimStart('0').ToUpper();
                                ulong recoveryEcidUlong = Convert.ToUInt64(recoveryEcid, 16);
                                MessageBox.Show("Device in recovery mode with ECID: " + recoveryEcidUlong);
                                recoveryDevice.irecv_open_with_ecid(out recoveryClientHandle, recoveryEcidUlong).ThrowOnError();
                                Dispatcher.Invoke(() =>
                                {
                                    // set the tab control to visible
                                    dashboard.Visibility = Visibility.Hidden;
                                    connectToPC.Visibility = Visibility.Hidden;
                                    recoveryMode.Visibility = Visibility.Visible;
                                    recoveryModeEcid.Content = "ECID: " + recoveryEcidUlong;
                                });
                                isconnected = true;
                            }
                        }
                    }
                    continue;
                }
                // get the first device
                string device = deviceList[0];
                iDevice.idevice_new(out deviceHandle, device);
                lockdown.lockdownd_client_new_with_handshake(deviceHandle, out lockdownClientHandle, "iDeviceToolbox");
                Dispatcher.Invoke(() => {
                    dashboard.Visibility = Visibility.Visible;
                    lockdown.lockdownd_get_device_name(lockdownClientHandle, out string deviceNameText);
                    deviceName.Content = deviceNameText;
                    lockdown.lockdownd_get_device_udid(lockdownClientHandle, out string deviceUdidText);
                    udid.Content = deviceUdidText;
                    // software version
                    lockdown.lockdownd_get_value(lockdownClientHandle, null, "ProductVersion", out PlistHandle softwareVersion);
                    softwareVersion.Api.Plist.plist_get_string_val(softwareVersion, out string softwareVersionText);
                    softwareVer.Content = softwareVersionText;
                    // storage space
                    lockdown.lockdownd_get_value(lockdownClientHandle, "com.apple.disk_usage", "TotalDataAvailable", out PlistHandle storageSpaceAvailable);
                    // TotalDataCapacity
                    lockdown.lockdownd_get_value(lockdownClientHandle, "com.apple.disk_usage", "TotalDataCapacity", out PlistHandle storageSpaceTotal);
                    ulong storageSpaceAvailableInt = 0;
                    ulong storageSpaceTotalInt = 0;
                    storageSpaceAvailable.Api.Plist.plist_get_uint_val(storageSpaceAvailable, ref storageSpaceAvailableInt);
                    storageSpaceTotal.Api.Plist.plist_get_uint_val(storageSpaceTotal, ref storageSpaceTotalInt);
                    storageSpaceShown.Content = $"{storageSpaceAvailableInt / 1024 / 1024} MB / {storageSpaceTotalInt / 1024 / 1024} MB";
                    // product type (e.g iPhone8,1)
                    lockdown.lockdownd_get_value(lockdownClientHandle, null, "ProductType", out PlistHandle productType);
                    productType.Api.Plist.plist_get_string_val(productType, out string productTypeText);
                    productTypeShown.Content = productTypeText;
                    isconnected = true;
                    infoContent.Visibility = Visibility.Visible;
                    recoveryContent.Visibility = Visibility.Hidden;
                    restoreContent.Visibility = Visibility.Hidden;
                    utilitiesContent.Visibility = Visibility.Hidden;
                    syslogContent.Visibility = Visibility.Hidden;
                    jailbreaksContent.Visibility = Visibility.Hidden;
                    aboutContent.Visibility = Visibility.Hidden;
                    versionLabel.Content = "v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                });

            }
            // TODO: add device disconnect detection and proper handling
        }
        private void infoButton_Click(object sender, RoutedEventArgs e) {
            // show the device info page and hide the other pages
            infoContent.Visibility = Visibility.Visible;
            recoveryContent.Visibility = Visibility.Hidden;
            restoreContent.Visibility = Visibility.Hidden;
            utilitiesContent.Visibility = Visibility.Hidden;
            syslogContent.Visibility = Visibility.Hidden;
            jailbreaksContent.Visibility = Visibility.Hidden;
            aboutContent.Visibility = Visibility.Hidden;
        }
        private void recoveryButton_Click(object sender, RoutedEventArgs e) {
            // show the recovery page and hide the other pages
            infoContent.Visibility = Visibility.Hidden;
            recoveryContent.Visibility = Visibility.Visible;
            restoreContent.Visibility = Visibility.Hidden;
            utilitiesContent.Visibility = Visibility.Hidden;
            syslogContent.Visibility = Visibility.Hidden;
            jailbreaksContent.Visibility = Visibility.Hidden;
            aboutContent.Visibility = Visibility.Hidden;
        }
        private void restoreButton_Click(object sender, RoutedEventArgs e) {
            // show the restore page and hide the other pages
            infoContent.Visibility = Visibility.Hidden;
            recoveryContent.Visibility = Visibility.Hidden;
            restoreContent.Visibility = Visibility.Visible;
            utilitiesContent.Visibility = Visibility.Hidden;
            syslogContent.Visibility = Visibility.Hidden;
            jailbreaksContent.Visibility = Visibility.Hidden;
            aboutContent.Visibility = Visibility.Hidden;
        }

        private void utilitiesButton_Click(object sender, RoutedEventArgs e) {
            infoContent.Visibility = Visibility.Hidden;
            recoveryContent.Visibility = Visibility.Hidden;
            restoreContent.Visibility = Visibility.Hidden;
            utilitiesContent.Visibility = Visibility.Visible;
            syslogContent.Visibility = Visibility.Hidden;
            jailbreaksContent.Visibility = Visibility.Hidden;
            aboutContent.Visibility = Visibility.Hidden;
        }

        private void syslogButton_Click(object sender, RoutedEventArgs e) {
            infoContent.Visibility = Visibility.Hidden;
            recoveryContent.Visibility = Visibility.Hidden;
            restoreContent.Visibility = Visibility.Hidden;
            utilitiesContent.Visibility = Visibility.Hidden;
            syslogContent.Visibility = Visibility.Visible;
            jailbreaksContent.Visibility = Visibility.Hidden;
            aboutContent.Visibility = Visibility.Hidden;
            using (Process p = new Process())
            {
                p.StartInfo.FileName = AppDomain.CurrentDomain.BaseDirectory + "win-x64\\idevicesyslog.exe";
                p.StartInfo.Arguments = "-u " + udid.Content;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.OutputDataReceived += new DataReceivedEventHandler((sender2, e2) => {
                    if (e2.Data != null)
                    {
                        Dispatcher.Invoke(() => {
                            syslogBox.AppendText(e2.Data + Environment.NewLine);
                            syslogBox.ScrollToEnd();
                        });
                    }
                });
                p.Start();
                p.BeginOutputReadLine();

            }
            syslogContent.IsVisibleChanged += (sender2, e2) => {
                foreach (Process p in Process.GetProcessesByName("idevicesyslog"))
                {
                    p.Kill();
                }
            };
        }

        private void jailbreaksButton_Click(object sender, RoutedEventArgs e) {
            infoContent.Visibility = Visibility.Hidden;
            recoveryContent.Visibility = Visibility.Hidden;
            restoreContent.Visibility = Visibility.Hidden;
            utilitiesContent.Visibility = Visibility.Hidden;
            syslogContent.Visibility = Visibility.Hidden;
            jailbreaksContent.Visibility = Visibility.Visible;
            aboutContent.Visibility = Visibility.Hidden;
            lockdown.lockdownd_get_value(lockdownClientHandle, null, "ProductVersion", out PlistHandle softwareVersion);
            softwareVersion.Api.Plist.plist_get_string_val(softwareVersion, out string softwareVersionText);
            // iOS 15.0 - 15.4.1 are compatible with trollstore
            if (softwareVersionText.StartsWith("15.0") || softwareVersionText.StartsWith("15.1") || softwareVersionText.StartsWith("15.2") || softwareVersionText.StartsWith("15.3") || softwareVersionText.StartsWith("15.4")) {
                jailbreak = "TrollStore";
            }
            jailbreakisCompatible.Content = "Compatible with: " + jailbreak + " (iOS " + softwareVersionText + ")";
        }

        private void aboutButton_Click(object sender, RoutedEventArgs e) {
            infoContent.Visibility = Visibility.Hidden;
            recoveryContent.Visibility = Visibility.Hidden;
            restoreContent.Visibility = Visibility.Hidden;
            utilitiesContent.Visibility = Visibility.Hidden;
            syslogContent.Visibility = Visibility.Hidden;
            jailbreaksContent.Visibility = Visibility.Hidden;
            aboutContent.Visibility = Visibility.Visible;
        }

        private async void jailbreaksButton1_Click(object sender, RoutedEventArgs e) {
            if (jailbreak == "TrollStore") {
                jailbreaksBox.AppendText("Downloading TrollInstaller.ipa..." + Environment.NewLine);
                jailbreaksBox.AppendText("This may take a while..." + Environment.NewLine);
                using (WebClient client = new WebClient())
                {
                    client.DownloadProgressChanged += (sender2, e2) =>
                    {
                        jailbreaksBox.AppendText("Downloaded " + e2.BytesReceived + " of " + e2.TotalBytesToReceive + " bytes." + Environment.NewLine);
                        jailbreaksBox.ScrollToEnd();
                    };
                    await client.DownloadFileTaskAsync("https://jailbreaks.app/cdn/ipas/TrollHelper.ipa", AppDomain.CurrentDomain.BaseDirectory + "TrollHelper.ipa");
                }

                jailbreaksBox.AppendText("Downloaded TrollInstaller.ipa" + Environment.NewLine);
                jailbreaksBox.AppendText("Installing TrollInstaller.ipa..." + Environment.NewLine);
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = AppDomain.CurrentDomain.BaseDirectory + "win-x64\\ideviceinstaller.exe";
                    p.StartInfo.Arguments = "-u " + udid.Content + " -i " + AppDomain.CurrentDomain.BaseDirectory + "TrollHelper.ipa";
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.OutputDataReceived += new DataReceivedEventHandler((sender2, e2) =>
                    {
                        if (e2.Data != null)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                jailbreaksBox.AppendText(e2.Data + Environment.NewLine);
                                jailbreaksBox.ScrollToEnd();
                                if (e2.Data.Contains("Complete")) {
                                    Dispatcher.Invoke(() =>
                                    {
                                        jailbreaksBox.AppendText("Done, Please open the GTA Car Tracker app" + Environment.NewLine + "and click the Install button" + Environment.NewLine);
                                        jailbreaksBox.ScrollToEnd();
                                    });
                                }
                            });
                        }
                    });
                    p.Start();
                    p.BeginOutputReadLine();
                }
            }
            else {
                jailbreaksBox.AppendText("This iOS version is not supported." + Environment.NewLine);
            }
        }

        private void hideSensitiveData_Checked(object sender, RoutedEventArgs e) {
            // hide the sensitive data
            udid.Content = "********";
        }
        private void hideSensitiveData_Unchecked(object sender, RoutedEventArgs e) {
            // show the sensitive data
            lockdown.lockdownd_get_device_udid(lockdownClientHandle, out string deviceUdidText);
            udid.Content = deviceUdidText;
        }
        // restoreButton2_Click
        private void restoreButton2_Click(object sender, RoutedEventArgs e) {
            MessageBox.Show("This feature is not implemented yet, stay tuned!", "iDeviceToolbox", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void exitRecoveryModeButton_Click(object sender, RoutedEventArgs e) {
            recoveryDevice.irecv_setenv(recoveryClientHandle, "auto-boot", "true");
            recoveryDevice.irecv_saveenv(recoveryClientHandle);
            recoveryDevice.irecv_reboot(recoveryClientHandle);
            Application.Current.Shutdown();
        }
        private void recoveryButton1_Click(object sender, RoutedEventArgs e)
        {
            // put device in recovery mode
            lockdown.lockdownd_enter_recovery(lockdownClientHandle);
        }
        private void enableAssistiveTouch(object sender, RoutedEventArgs e)
        {
            // enable assistive touch (com.apple.Accessibility.AssistiveTouchEnabledByiTunes)
            lockdown.lockdownd_get_value(lockdownClientHandle, "com.apple.Accessibility", "AssistiveTouchEnabledByiTunes", out PlistHandle temp).ThrowOnError();
            char assistiveTouchEnabled = '\0';
            temp.Api.Plist.plist_get_bool_val(temp, ref assistiveTouchEnabled);
            temp.Api.Plist.plist_set_bool_val(temp, '1');
            lockdown.lockdownd_set_value(lockdownClientHandle, "com.apple.Accessibility", "AssistiveTouchEnabledByiTunes", temp).ThrowOnError();
        }
        private void disableAssistiveTouch(object sender, RoutedEventArgs e)
        {
            // disable assistive touch (com.apple.Accessibility.AssistiveTouchEnabledByiTunes)
            lockdown.lockdownd_get_value(lockdownClientHandle, "com.apple.Accessibility", "AssistiveTouchEnabledByiTunes", out PlistHandle temp).ThrowOnError();
            char assistiveTouchEnabled = '\0';
            temp.Api.Plist.plist_get_bool_val(temp, ref assistiveTouchEnabled);
            temp.Api.Plist.plist_set_bool_val(temp, '\0');
            lockdown.lockdownd_set_value(lockdownClientHandle, "com.apple.Accessibility", "AssistiveTouchEnabledByiTunes", temp).ThrowOnError();
        }
        // startSSHTunnel
        private void startSSHTunnel(object sender, RoutedEventArgs e)
        {
            // start the ssh tunnel
            using (Process p = new Process())
            {
                p.StartInfo.FileName = AppDomain.CurrentDomain.BaseDirectory + "win-x64\\iproxy.exe";
                p.StartInfo.Arguments = "2222 44";
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.Start();

                // now launch the ssh client (built-in windows 10 ssh client) in a new window
                Process.Start("ssh", "root@localhost -p 2222");

                // close the process when navigating away from the page
                utilitiesContent.IsVisibleChanged += (sender2, e2) => {
                    foreach (Process p2 in Process.GetProcessesByName("iproxy"))
                    {
                        p2.Kill();
                    }
                };

            }
        }
    }
}
