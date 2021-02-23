using SyntropyNet.WindowsApp.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SyntropyNet.WindowsApp.Helpers
{
    public class WindowHelpers
    {
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

        public static (int,int) GetWindowPosition(bool useCursor = false)
        {
            int left = 0;
            int top = 0;
            var taskBarPos = GetTaskBarLocation();
            switch (taskBarPos) { 
                case TaskBarLocation.BOTTOM:
                    left = useCursor ? Cursor.Position.X - 320 : Screen.PrimaryScreen.WorkingArea.Right - 380;
                    top = Screen.PrimaryScreen.WorkingArea.Bottom - 475;
                    break;
                case TaskBarLocation.TOP:
                    left = useCursor ? Cursor.Position.X - 320 : Screen.PrimaryScreen.WorkingArea.Right - 380;
                    top = Screen.PrimaryScreen.WorkingArea.Top;
                    break;
                case TaskBarLocation.LEFT:
                    left = Screen.PrimaryScreen.WorkingArea.Left;
                    top = useCursor ? Cursor.Position.Y - 450 : Screen.PrimaryScreen.WorkingArea.Bottom - 480;
                    break;
                case TaskBarLocation.RIGHT:
                    left = Screen.PrimaryScreen.WorkingArea.Right - 350;
                    top = useCursor ? Cursor.Position.Y - 450 : Screen.PrimaryScreen.WorkingArea.Bottom - 480;
                    break;
            }

            return (left,top);
        }
    }
}
