using log4net;
using SyntropyNet.WindowsApp.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace SyntropyNet.WindowsApp.Helpers
{
    public class WindowHelpers
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WindowHelpers));
        public static TaskBarLocation GetTaskBarLocation()
        {
            TaskBarLocation taskBarLocation = TaskBarLocation.BOTTOM;
            bool taskBarOnTopOrBottom = (Screen.PrimaryScreen.WorkingArea.Width == Screen.PrimaryScreen.Bounds.Width);
            if (taskBarOnTopOrBottom)
            {
                if (Screen.PrimaryScreen.WorkingArea.Top > 0) taskBarLocation = TaskBarLocation.TOP;
            }
            else
            {
                if (Screen.PrimaryScreen.WorkingArea.Left > 0)
                {
                    taskBarLocation = TaskBarLocation.LEFT;
                }
                else
                {
                    taskBarLocation = TaskBarLocation.RIGHT;
                }
            }
            return taskBarLocation;
        }

        public static (int,int) GetWindowPosition(Window widnow, bool useCursor = false)
        {
            int left = 0;
            int top = 0;
            var taskBarPos = GetTaskBarLocation();
            var source = PresentationSource.FromVisual(widnow);
            double scaleX = 1;
            double scaleY = 1;
            if (source != null)
            {
                try { 
                    var scaleDetails = source.CompositionTarget.TransformToDevice;
                    scaleX = scaleDetails.M11;
                    scaleY = scaleDetails.M22;
                }
                catch
                {

                }
            }
            switch (taskBarPos) { 
                case TaskBarLocation.BOTTOM:
                    left = useCursor ? Cursor.Position.X - 320 : (int)(Screen.PrimaryScreen.WorkingArea.Right / scaleX) - 380;
                    top = (int)(Screen.PrimaryScreen.WorkingArea.Bottom / scaleY) - 475 ;
                    break;
                case TaskBarLocation.TOP:
                    left = useCursor ? Cursor.Position.X - 320 : (int)(Screen.PrimaryScreen.WorkingArea.Right / scaleX) - 380;
                    top = (int)(Screen.PrimaryScreen.WorkingArea.Top / scaleY);
                    break;
                case TaskBarLocation.LEFT:
                    left = (int)(Screen.PrimaryScreen.WorkingArea.Left / scaleX);
                    top = useCursor ? Cursor.Position.Y - 450 : (int)(Screen.PrimaryScreen.WorkingArea.Bottom / scaleY) - 480;
                    break;
                case TaskBarLocation.RIGHT:
                    left = (int)(Screen.PrimaryScreen.WorkingArea.Right / scaleX) - 350;
                    top = useCursor ? Cursor.Position.Y - 450 : (int)(Screen.PrimaryScreen.WorkingArea.Bottom / scaleY) - 480;
                    break;
            }

            return (left, top);
        }
    }
}
