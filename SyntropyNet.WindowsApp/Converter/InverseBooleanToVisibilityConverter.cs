using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace SyntropyNet.WindowsApp.Converter
{
    [Localizability(LocalizationCategory.NeverLocalize)]
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        //
        // Summary:
        //     Converts a Boolean value to a System.Windows.Visibility enumeration value.
        //
        // Parameters:
        //   value:
        //     The Boolean value to convert. This value can be a standard Boolean value or a
        //     nullable Boolean value.
        //
        //   targetType:
        //     This parameter is not used.
        //
        //   parameter:
        //     This parameter is not used.
        //
        //   culture:
        //     This parameter is not used.
        //
        // Returns:
        //     System.Windows.Visibility.Visible if value is true; otherwise, System.Windows.Visibility.Collapsed.
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = false;
            if (value is bool)
            {
                flag = (bool)value;
            }
            else if (value is bool?)
            {
                bool? flag2 = (bool?)value;
                flag = flag2.HasValue && flag2.Value;
            }

            return (flag) ? Visibility.Collapsed : Visibility.Visible;
        }

        //
        // Summary:
        //     Converts a System.Windows.Visibility enumeration value to a Boolean value.
        //
        // Parameters:
        //   value:
        //     A System.Windows.Visibility enumeration value.
        //
        //   targetType:
        //     This parameter is not used.
        //
        //   parameter:
        //     This parameter is not used.
        //
        //   culture:
        //     This parameter is not used.
        //
        // Returns:
        //     true if value is System.Windows.Visibility.Visible; otherwise, false.
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility)
            {
                return (Visibility)value == Visibility.Collapsed;
            }

            return true;
        }

        //
        // Summary:
        //     Initializes a new instance of the System.Windows.Controls.BooleanToVisibilityConverter
        //     class.
        public InverseBooleanToVisibilityConverter()
        {
        }
    }
}
