using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;

namespace SyntropyNet.WindowsApp.Converter
{
    public class StarWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            ListView listview = value as ListView;
            double width = listview.ActualWidth;
            GridView gv = listview.View as GridView;
            for (int i = 0; i < gv.Columns.Count; i++)
            {
                if (!Double.IsNaN(gv.Columns[i].Width))
                    width -= gv.Columns[i].Width;
            }
            return width - 15;// this is to take care of margin/padding
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
