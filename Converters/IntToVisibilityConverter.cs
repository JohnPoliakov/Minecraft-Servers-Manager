using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Minecraft_Server_Manager.Converters
{
    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                bool invert = parameter?.ToString() == "Inverted";

                if (invert)
                    return count == 0 ? Visibility.Visible : Visibility.Collapsed;

                return count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}