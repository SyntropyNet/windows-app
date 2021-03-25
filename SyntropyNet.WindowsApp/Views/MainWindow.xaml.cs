using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
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
using FontAwesome.WPF;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Services.ApiWrapper;
using SyntropyNet.WindowsApp.Helpers;
using Websocket.Client;
using Websocket.Client.Exceptions;

namespace SyntropyNet.WindowsApp.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Tray Icon
        private System.Windows.Forms.NotifyIcon m_notifyIcon;

        private void PrepareNotifyIcon()
        {
            // Tray Icon setup
            m_notifyIcon = new System.Windows.Forms.NotifyIcon();
            m_notifyIcon.BalloonTipText = "The app has been minimised. Click the tray icon to show.";
            m_notifyIcon.BalloonTipTitle = "Syntropy";
            m_notifyIcon.Text = "Syntropy";
            m_notifyIcon.Icon = new System.Drawing.Icon("syntropy-icon.ico");
            m_notifyIcon.Click += new EventHandler(m_notifyIcon_Click);
            m_notifyIcon.Visible = true;
            var trayMenu = new System.Windows.Forms.ContextMenu();
            var quitItem = new System.Windows.Forms.MenuItem();

            // Initialize contextMenu1
            trayMenu.MenuItems.AddRange(
                        new System.Windows.Forms.MenuItem[] { quitItem });

            // Initialize menuItem1
            quitItem.Index = 0;
            quitItem.Text = "Q&uit";
            quitItem.Click += new System.EventHandler(TrayQuit_Click);
            m_notifyIcon.ContextMenu = trayMenu;
        }

        private WindowState m_storedWindowState = WindowState.Normal;
        void OnStateChanged(object sender, EventArgs args)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                if (m_notifyIcon != null)
                    m_notifyIcon.ShowBalloonTip(2000);
            }
            else
                m_storedWindowState = WindowState;
        }
        void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            //CheckTrayIcon();
        }

        void m_notifyIcon_Click(object sender, EventArgs e)
        {
            var pos = WindowHelpers.GetWindowPosition(true);
            this.Left = pos.Item1;
            this.Top = pos.Item2;
            Show();
            WindowState = m_storedWindowState;
        }
        void CheckTrayIcon()
        {
            ShowTrayIcon(!IsVisible);
        }

        void ShowTrayIcon(bool show)
        {
            if (m_notifyIcon != null)
                m_notifyIcon.Visible = show;
        }

        #endregion
        private readonly IAppSettings _appSettings;
        public MainWindow(IAppSettings appSettings)
        {
            _appSettings = appSettings;
            PrepareNotifyIcon();
            var pos = WindowHelpers.GetWindowPosition();
            this.Left = pos.Item1;
            this.Top = pos.Item2;
            this.Topmost = true;
            InitializeComponent();
            BrushConverter bc = new BrushConverter();
            addTokenBtn.Background = (Brush)bc.ConvertFrom("#0178d4");
        }

        void OnClose(object sender, CancelEventArgs args)
        {
            m_notifyIcon.Visible = false;
            m_notifyIcon.Dispose();
            m_notifyIcon = null;
        }

        private void ImageAwesome_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var rectangle = sender as Image;
                ContextMenu contextMenu = rectangle.ContextMenu;
                contextMenu.PlacementTarget = rectangle;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Left;
                contextMenu.IsOpen = true;
            }
        }

        private void Copy_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if(sender is Image)
            {
                System.Windows.Forms.Clipboard.SetText((sender as Image).Tag.ToString());
            }
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            if (!_appSettings.ModalWindowActivated)
            {
                WindowState = WindowState.Minimized;
            }
        }

        private void TrayQuit_Click(object Sender, EventArgs e)
        {
            // Close the form, which closes the application.
            this.Close();
        }

        private void Quit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
