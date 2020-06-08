using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WebSocket4Net;
using System.Web.Script.Serialization;
using System.Windows.Media.Animation;

namespace Pro7RemoteMiddleMan
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        WebSocket MasterWebSocket; // WebSocket connection to Pro7 (treated as "Master")
        WebSocket SlaveWebSocket; // WebSocket connection to Pro7 (treated as "Slave")
        String MasterSettings = ""; // Keeping track of settings used to connect (so we can re-connect if they change)
        String SlaveSettings = ""; // Keeping track of settings used to connect (so we can re-connect if they change)
        String CurrentPresentationPath;  // This is kept as part of the logic for a work-around to avoid hanging Pro7 (Never send a presentationTriggerIndex message that targets a presentation that is not currently active without first sending a presentationRequest message!)

        //websocket.Send("{\"action\":\"presentationTriggerNext\"}");

        public MainWindow()
        {
            InitializeComponent();
        }

        #region Window Events
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // MoveFocus controls to suit window size
            UpdateControlsLayout();

            //  watchDogTimer will check connections once per second and try to re-connect if not connected.
            System.Windows.Threading.DispatcherTimer watchDogTimer = new System.Windows.Threading.DispatcherTimer();
            watchDogTimer.Tick += WatchDogTimer_Tick;
            watchDogTimer.Interval = new TimeSpan(0, 0, 1);
            watchDogTimer.Start();

            // In case settings get corrupt and we end up say zero dimension window - let's default back to small size
            if (this.Width < 185)
                this.Width = 185;
            
            if (this.Height < 40)
                this.Height = 40;

        }

        private void WatchDogTimer_Tick(object sender, EventArgs e)
        {
            // TODO: Consider putting in an exit clause here to avoid re-connecting until 2-3 seconds after editing settings??

            // If settings have changed or we are not connected then (re)establish Master connection
            if (MasterWebSocket == null || (MasterWebSocket.State == WebSocketState.Closed || MasterWebSocket.State == WebSocketState.None || MasterSettings != Properties.Settings.Default.MasterNetworkAddress + Properties.Settings.Default.MasterNetworkPort + Properties.Settings.Default.MasterPassword))
            {
                // Kill existing connection (in case where properties have changed but we have an open connection)
                if (MasterWebSocket != null && (MasterWebSocket.State == WebSocketState.Open || MasterWebSocket.State == WebSocketState.Connecting))
                {
                    MasterWebSocket.Close();
                    MasterWebSocket.Opened -= MasterWebSocket_Opened;
                    MasterWebSocket.Closed -= MasterWebSocket_Closed;
                    MasterWebSocket.Error -= MasterWebSocket_Error;
                    MasterWebSocket = null;
                    
                }
                    
                MasterWebSocket = new WebSocket("ws://" + Properties.Settings.Default.MasterNetworkAddress + ":" + Properties.Settings.Default.MasterNetworkPort + "/remote");
                MasterWebSocket.Opened += MasterWebSocket_Opened;
                MasterWebSocket.Closed += MasterWebSocket_Closed;
                MasterWebSocket.Error += MasterWebSocket_Error;
                MasterWebSocket.MessageReceived += MasterWebSocket_MessageReceived;
                MasterWebSocket.Open();
                MasterSettings = Properties.Settings.Default.MasterNetworkAddress + Properties.Settings.Default.MasterNetworkPort + Properties.Settings.Default.MasterPassword;
            }


            // If settings have changed or we are not connected then (re)establish Slave connection
            if (SlaveWebSocket == null  || (SlaveWebSocket.State == WebSocketState.Closed || SlaveWebSocket.State == WebSocketState.None || SlaveSettings != Properties.Settings.Default.SlaveNetworkAddress + Properties.Settings.Default.SlaveNetworkPort + Properties.Settings.Default.SlavePassword))
            {
                // Kill existing connection (in case where properties have changed but we have an open connection)
                if (SlaveWebSocket != null && (SlaveWebSocket.State == WebSocketState.Open || SlaveWebSocket.State == WebSocketState.Connecting))
                {
                    SlaveWebSocket.Close();
                    SlaveWebSocket.Opened -= SlaveWebSocket_Opened;
                    SlaveWebSocket.Closed -= SlaveWebSocket_Closed;
                    SlaveWebSocket.Error -= SlaveWebSocket_Error;
                    SlaveWebSocket = null;
                }

                SlaveWebSocket = new WebSocket("ws://" + Properties.Settings.Default.SlaveNetworkAddress + ":" + Properties.Settings.Default.SlaveNetworkPort + "/remote");
                SlaveWebSocket.Opened += SlaveWebSocket_Opened;
                SlaveWebSocket.Closed += SlaveWebSocket_Closed;
                SlaveWebSocket.Error += SlaveWebSocket_Error;
                SlaveWebSocket.MessageReceived += SlaveWebSocket_MessageReceived;
                SlaveWebSocket.Open();
                SlaveSettings = Properties.Settings.Default.SlaveNetworkAddress + Properties.Settings.Default.SlaveNetworkPort + Properties.Settings.Default.SlavePassword;
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.Save();
        }

        private void btnToggleSize_Click(object sender, RoutedEventArgs e)
        {
            // Toggle Window Size between small and large to hide/reveal extra controls.
            if (this.Width > 400) // I  know - it's a crude way to check if the window is small or large..
            {
                // Set to SMALL window size
                this.Width = 185;
                this.Height = 40;

                UpdateControlsLayout();

            }
            else
            {
                // Set to LARGE window size
                this.Width = 541;
                this.Height = 370;

                UpdateControlsLayout();

            }

        }

        private void btnQuit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        #endregion

        #region WebSocket Events
        private void MasterWebSocket_Opened(object sender, EventArgs e)
        {
            // Update Master connection indicator to yellow (connected - not yet authenticated)
            Dispatcher.Invoke(() =>
            {
                MasterConnectionIndicatorRectangle.Fill = new SolidColorBrush(Color.FromRgb(255, 255, 0));
                AddLog("Master WebSocket Connected:");
            });

            

            // Send authentication message
            MasterWebSocket.Send("{\"action\":\"authenticate\",\"protocol\":\"700\",\"password\":\"" + Properties.Settings.Default.MasterPassword  + "\"}");
        }

        private void MasterWebSocket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                AddLog("Master Said: " + e.Message); ;
                // "Flicker" indicators when messages arrive...
                ColorAnimation colorAnimation = new ColorAnimation();
                colorAnimation.From = Color.FromRgb(0, 255, 0);
                colorAnimation.To = Color.FromRgb(128, 128, 128);
                colorAnimation.AutoReverse = true;
                colorAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(100));
                colorAnimation.FillBehavior = FillBehavior.Stop;
                MasterConnectionIndicatorRectangle.Fill.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
            });

            

            // Get Dictionary representation of JSON message object objects with string keys
            var JSONObjMessage = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(e.Message);
            if (JSONObjMessage.ContainsKey("action"))
            {
                switch (JSONObjMessage["action"])
                {
                    case "authenticate":
                        {
                            if (JSONObjMessage["authenticated"].ToString() == "1")  // Authenication success!
                            {
                                // Update Master connection indicator to green (connected & authenticated)
                                Dispatcher.Invoke(() =>
                                {
                                    MasterConnectionIndicatorRectangle.Fill = new SolidColorBrush(Color.FromRgb(0, 255, 0));
                                });
                            }
                        }
                        break;
                    case "presentationTriggerIndex":
                        {
                            String slaveTriggercommand = "";

                            // "Master" has triggered a slide. let's send trigger to all slaves
                            String slideIndex = JSONObjMessage["slideIndex"].ToString();
                            String presentationPath = JSONObjMessage["presentationPath"].ToString();
                            if (SlaveWebSocket.State == WebSocketState.Open)
                            {
                                slaveTriggercommand = "{\"action\":\"presentationTriggerIndex\",\"slideIndex\":\"" + slideIndex + "\",\"presentationPath\":\"" + presentationPath + "\"}";
                                Dispatcher.Invoke(() =>
                                {
                                    AddLog("Telling Slave: " + slaveTriggercommand);
                                    // "Flicker" arrow when sending slave a message...
                                    ColorAnimation colorAnimation = new ColorAnimation();
                                    colorAnimation.From = Color.FromRgb(0, 255, 0);
                                    colorAnimation.To = Color.FromRgb(128, 128, 128);
                                    colorAnimation.AutoReverse = true;
                                    colorAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(100));
                                    colorAnimation.FillBehavior = FillBehavior.Stop;
                                    txtArrows.Foreground.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
                                });
                               
                                // ALERT: to avoid Pro7 hanging/crash we do a workaround:
                                // Always make sure if we are about to trigger a slide for a NEW presentationPath then send presentationRequest FIRST before presentationTriggerIndex
                                // We do this by recording the currentPresentationPath and comparing before every triggered slide.
                                if (CurrentPresentationPath != presentationPath)
                                {
                                    SlaveWebSocket.Send("{\"action\": \"presentationRequest\",\"presentationPath\": \"" + presentationPath + "\",\"presentationSlideQuality\": \"0\"}");

                                    // wait a bit - hopefully pro7 slave "catches up here" and then send trigger command
                                    Task.Factory.StartNew(() =>
                                    {
                                        System.Threading.Thread.Sleep(500);  //TODO: if we get stuck with this terrible workaround - maybe make this configurable.. (or better still, let's also enumarate the playlist upon connection and just call presenationRequest on *everything* to avoid this delay)
                                        CurrentPresentationPath = presentationPath;
                                        SlaveWebSocket.Send(slaveTriggercommand);
                                    });
                                }
                                else
                                {
                                    SlaveWebSocket.Send(slaveTriggercommand);
                                }


                            }
                        }
                        break;
                    default:
                        break;
                }
                
            }
        }

        private void MasterWebSocket_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
               // Update Master connection indicator to red
            Dispatcher.Invoke(() =>
            {
                MasterConnectionIndicatorRectangle.Fill = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                AddLog("Master WebSocket Error: " + e.Exception.Message);
            });
            
            
        }

        private void MasterWebSocket_Closed(object sender, EventArgs e)
        {
            // Update Master connection indicator to red
            Dispatcher.Invoke(() =>
            {
                MasterConnectionIndicatorRectangle.Fill = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                AddLog("Master WebSocket Closed:");
            });

            
        }


        private void SlaveWebSocket_Opened(object sender, EventArgs e)
        {
            // Update Slave connection indicator to yellow (connected - not yet authenticated)
            Dispatcher.Invoke(() =>
            {
                SlaveConnectionIndicatorRectangle.Fill = new SolidColorBrush(Color.FromRgb(255, 255, 0));
                AddLog("Slave WebSocket Connected:");
            });

            

            // Send authentication message
            SlaveWebSocket.Send("{\"action\":\"authenticate\",\"protocol\":\"700\",\"password\":\"" + Properties.Settings.Default.SlavePassword  + "\"}");
        }

        private void SlaveWebSocket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                AddLog("Slave Said: " + e.Message);
                // "Flicker" indicators when messages arrive...
                ColorAnimation colorAnimation = new ColorAnimation();
                colorAnimation.From = Color.FromRgb(0, 255, 0);
                colorAnimation.To = Color.FromRgb(128, 128, 128);
                colorAnimation.AutoReverse = true;
                colorAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(100));
                colorAnimation.FillBehavior = FillBehavior.Stop;
                SlaveConnectionIndicatorRectangle.Fill.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
            });
            
            
            // Get Dictionary representation of JSON message object objects with string keys
            var JSONObjMessage = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(e.Message.ToString());
            if (JSONObjMessage.ContainsKey("action"))
            {
                switch (JSONObjMessage["action"])
                {
                    case "authenticate":
                        {
                            if (JSONObjMessage["authenticated"].ToString() == "1")  // Authenication success!
                            {
                                // Update Slave connection indicator to green (connected & authenticated)
                                Dispatcher.Invoke(() =>
                                {
                                    SlaveConnectionIndicatorRectangle.Fill = new SolidColorBrush(Color.FromRgb(0, 255, 0));
                                });
                            }
                        }
                        break;
                    case "presentationTriggerIndex":
                        // TODO: Consider to use this as confirmation that the slave responded to a previous request to trigger a slide in a presenatation.
                        // This might be tricky to manage state if user "blasts" through slides in master much quick than network round trip - we probably can't always wait for an acknowledgement of every triggered slide that send requests to the slave for.
                        // But it would be nice and useful to update slave indicator with special colour at some point when too many requested slides are not acknowledged (slave is probably not in sync with master - eg when playlists dont match)
                        break;
                    case "presentationCurrent":
                        break;
                    default:
                        break;
                }

            }
            
        }

        private void SlaveWebSocket_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            // Update Slave connection indicator to red
            Dispatcher.Invoke(() =>
            {
                SlaveConnectionIndicatorRectangle.Fill = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                AddLog("Slave WebSocket Error: " + e.Exception.Message);
            });

            
        }

        private void SlaveWebSocket_Closed(object sender, EventArgs e)
        {
            // Update Slave connection indicator to red
            Dispatcher.Invoke(() =>
            {
                SlaveConnectionIndicatorRectangle.Fill = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                AddLog("Slave WebSocket Closed:");
            });

            
        }
        #endregion

        private void AddLog(String logString)
        {
            txtLog.AppendText(logString + System.Environment.NewLine);
            txtLog.Text = txtLog.Text.Last(500000);  // Let's only keep the last 500KB of log text.
            txtLog.ScrollToEnd();
        }

        private void UpdateControlsLayout()
        {
            if (this.Width > 400)
            {
                // Move around the status indicators to suit LARGE window size
                Thickness margin;
                // Move the M:
                margin = txtM.Margin;
                margin.Left = 91;
                margin.Top = 4;
                txtM.Margin = margin;
                // Move the Master indicator
                margin = MasterConnectionIndicatorRectangle.Margin;
                margin.Left = 124;
                margin.Top = 15;
                MasterConnectionIndicatorRectangle.Margin = margin;
                // Move the arrow text
                margin = txtArrows.Margin;
                margin.Left = 246;
                margin.Top = 73;
                txtArrows.Margin = margin;
                // Move the S:
                margin = txtS.Margin;
                margin.Left = 397;
                margin.Top = 4;
                txtS.Margin = margin;
                // Move the Slave indicator
                margin = SlaveConnectionIndicatorRectangle.Margin;
                margin.Left = 423;
                margin.Top = 15;
                SlaveConnectionIndicatorRectangle.Margin = margin;
            }
            else
            {
                // Move around the status indicators to suit SMALL window size
                Thickness margin;
                // Move the M:
                margin = txtM.Margin;
                margin.Left = 5;
                margin.Top = 0;
                txtM.Margin = margin;
                // Move the Master indicator
                margin = MasterConnectionIndicatorRectangle.Margin;
                margin.Left = 35;
                margin.Top = 11;
                MasterConnectionIndicatorRectangle.Margin = margin;
                // Move the arrow text
                margin = txtArrows.Margin;
                margin.Left = 55;
                margin.Top = -2;
                txtArrows.Margin = margin;
                // Move the S:
                margin = txtS.Margin;
                margin.Left = 100;
                margin.Top = 0;
                txtS.Margin = margin;
                // Move the Slave indicator
                margin = SlaveConnectionIndicatorRectangle.Margin;
                margin.Left = 120;
                margin.Top = 11;
                SlaveConnectionIndicatorRectangle.Margin = margin;
            }

        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // A double click near top of window = toggle window size (just call the toogle button click)
            if (e.LeftButton == MouseButtonState.Pressed && e.GetPosition(this).Y < 50)
                btnToggleSize_Click(null, null);
        }
    }



    public static class StringExtension
    {
        public static string Last(this string source, int tail_length)
        {
            if (tail_length >= source.Length)
                return source;
            return source.Substring(source.Length - tail_length);
        }
    }

    public class SettingBindingExtension : Binding
    {
        public SettingBindingExtension()
        {
            Initialize();
        }

        public SettingBindingExtension(string path)
            : base(path)
        {
            Initialize();
        }

        private void Initialize()
        {
            this.Source = Properties.Settings.Default;
            this.Mode = BindingMode.TwoWay;
            this.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
        }
    }
}
