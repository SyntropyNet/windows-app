using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
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
using System.Windows.Threading;
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
        public static System.Windows.Forms.NotifyIcon m_notifyIcon;

        private void PrepareNotifyIcon()
        {
            // In rare cases we may have a "The root Visual of a VisualTarget cannot have a parent. " with Windows.Forms.NotifyIcon
            // tto avoid that we initialize and hide a tooltip before creating Windows.Forms.NotifyIcon;
            ToolTip tt = new ToolTip();
            tt.IsOpen = true;
            tt.IsOpen = false;
            // --

            this.ShowInTaskbar = false;
            // Tray Icon setup
            m_notifyIcon = new System.Windows.Forms.NotifyIcon();
            m_notifyIcon.BalloonTipText = "The app has been minimised. Click the tray icon to show.";
            m_notifyIcon.BalloonTipTitle = "Syntropy";
            m_notifyIcon.Text = "Syntropy";
            m_notifyIcon.Icon = new System.Drawing.Icon(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "syntropy-icon.ico"));
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
                _wpfContext.ShowBalloonTip("The app has been minimised. Click the tray icon to show.");
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
            var pos = WindowHelpers.GetWindowPosition(this);
            this.Left = pos.Item1;
            this.Top = pos.Item2;
            var a =this.Width;
            var b =this.ActualWidth;
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
        private readonly IContext _wpfContext;

        public MainWindow(IAppSettings appSettings, IContext wpfContext)
        {
            _wpfContext = wpfContext;
            _appSettings = appSettings;
            PrepareNotifyIcon();
            Loaded += (object sender, RoutedEventArgs e) =>
            {
                var pos = WindowHelpers.GetWindowPosition(this);
                this.Left = pos.Item1;
                this.Top = pos.Item2;
                
            };
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
                popup1.IsOpen = true;
                DispatcherTimer time = new DispatcherTimer();
                time.Interval = TimeSpan.FromMilliseconds(500);
                time.Start();
                time.Tick += delegate
                {
                    popup1.IsOpen = false;
                    time.Stop();
                };
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
