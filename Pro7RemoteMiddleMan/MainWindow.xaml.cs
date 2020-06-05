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

namespace Pro7RemoteMiddleMan
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        WebSocket MasterWebSocket; // WebSocket connection to Pro7 (treated as "Master")
        WebSocket SlaveWebSocket; // WebSocket connection to Pro7 (treated as "Slave")
        String CurrentPresentationPath;  // This is kept as part of the logic for a work-around to avoid hanging Pro7 (Never send a presentationTriggerIndex message that targets a presentation that is not currently active without first sending a presentationRequest message!)

        //websocket.Send("{\"action\":\"presentationTriggerNext\"}");

        public MainWindow()
        {
            InitializeComponent();
        }

        #region Window Events
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Open WebSocket connections once window is loaded.

            // TODO:
            // Update UI when connected/disconnected.
            // Add UI to set IP address/host and password.
            // Add code to save in settings
            // Add logic to watch connection and try to reconnect in background if connection fails    
            // "Flicker" indicators (and/or) arrows when messages are coming/going?

            MasterWebSocket = new WebSocket("ws://127.0.0.1:55556/remote");
            MasterWebSocket.Opened += MasterWebSocket_Opened;
            MasterWebSocket.Closed += MasterWebSocket_Closed;
            MasterWebSocket.Error += MasterWebSocket_Error;
            MasterWebSocket.MessageReceived += MasterWebSocket_MessageReceived;
            MasterWebSocket.Open();


            SlaveWebSocket = new WebSocket("ws://192.168.1.61:55555/remote");
            SlaveWebSocket.Opened += SlaveWebSocket_Opened;
            SlaveWebSocket.Closed += SlaveWebSocket_Closed;
            SlaveWebSocket.Error += SlaveWebSocket_Error;
            SlaveWebSocket.MessageReceived += SlaveWebSocket_MessageReceived;
            SlaveWebSocket.Open();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }
        #endregion

        #region WebSocket Events
        private void MasterWebSocket_Opened(object sender, EventArgs e)
        {
            // Update Master connection indicator to yellow (connected - not yet authenticated)
            Dispatcher.Invoke(() =>
            {
                MasterConnectionIndicatorRectangle.Fill = new SolidColorBrush(Color.FromRgb(255, 255, 0));
            });

            System.Diagnostics.Debug.Print("MasterWebSocket Connected:");

            // Send authentication message
            MasterWebSocket.Send("{\"action\":\"authenticate\",\"protocol\":\"700\",\"password\":\"control\"}"); // TODO: Use password from application settings/UI
        }

        private void MasterWebSocket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            System.Diagnostics.Debug.Print("MasterWebSocket Received Message: "  + e.Message);

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
                                System.Diagnostics.Debug.Print("Socket 2 Sending: " + slaveTriggercommand);
                                // ALERT: to avoid Pro7 hanging/crash we do a workaround:
                                // Always make sure if we are about to trigger a slide for a NEW presentationPath then send presentationRequest FIRST before presentationTriggerIndex
                                // We do this by recording the currentPresentationPath and comparing before every triggered slide.
                                if (CurrentPresentationPath != presentationPath)
                                {
                                    SlaveWebSocket.Send("{\"action\": \"presentationRequest\",\"presentationPath\": \"" + presentationPath + "\",\"presentationSlideQuality\": \"0\"}");
                                    CurrentPresentationPath = presentationPath;

                                    // wait a bit - hopefully pro7 slave "catches up here" and then send trigger command
                                    Task.Factory.StartNew(() =>
                                    {
                                        System.Threading.Thread.Sleep(500);  //TODO" if we get stuck with this terrible workaround - maybe make this configurable.. (or better still, let's also enumarate the playlist upon connection and just call presenationRequest on *everything* to avoid this delay)
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
            //TODO: Connection Watchdog will need to kick in here and try to re-establish connection to Master

            // Update Master connection indicator to red
            Dispatcher.Invoke(() =>
            {
                MasterConnectionIndicatorRectangle.Fill = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            });
            
            System.Diagnostics.Debug.Print("MasterWebSocket Error: " + e.Exception.Message);
        }

        private void MasterWebSocket_Closed(object sender, EventArgs e)
        {
            //TODO: Connection Watchdog will need to kick in here and try to re-establish connection to Master

            // Update Master connection indicator to red
            Dispatcher.Invoke(() =>
            {
                MasterConnectionIndicatorRectangle.Fill = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            });

            System.Diagnostics.Debug.Print("MasterWebSocket Closed:");
        }


        private void SlaveWebSocket_Opened(object sender, EventArgs e)
        {
            // Update Slave connection indicator to yellow (connected - not yet authenticated)
            Dispatcher.Invoke(() =>
            {
                SlaveConnectionIndicatorRectangle.Fill = new SolidColorBrush(Color.FromRgb(255, 255, 0));
            });

            System.Diagnostics.Debug.Print("SlaveWebSocket Connected:");

            // Send authentication message
            SlaveWebSocket.Send("{\"action\":\"authenticate\",\"protocol\":\"700\",\"password\":\"control\"}"); // TODO: Use password from application settings/UI
        }

        private void SlaveWebSocket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            System.Diagnostics.Debug.Print("SlaveWebSocket Received Message: " + e.Message);
            
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
            //TODO: Connection Watchdog will need to kick in here and try to re-establish connection to Slave

            // Update Slave connection indicator to red
            Dispatcher.Invoke(() =>
            {
                SlaveConnectionIndicatorRectangle.Fill = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            });

            System.Diagnostics.Debug.Print("SlaveWebSocket Error: " + e.Exception.Message);
        }

        private void SlaveWebSocket_Closed(object sender, EventArgs e)
        {
            //TODO: Connection Watchdog will need to kick in here and try to re-establish connection to Slave

            // Update Slave connection indicator to red
            Dispatcher.Invoke(() =>
            {
                SlaveConnectionIndicatorRectangle.Fill = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            });

            System.Diagnostics.Debug.Print("SlaveWebSocket Closed:");
        }
        #endregion

    }
}
