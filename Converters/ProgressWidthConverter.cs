using System.Globalization;
using System.Windows.Data;

namespace MusicPlayer.Converters
{
    public class ProgressWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values is [double value, double maximum and > 0, double actualWidth])
            {
                return (value / maximum) * actualWidth;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("ConvertBack is not supported for one-way binding.");
        }
    }
}

