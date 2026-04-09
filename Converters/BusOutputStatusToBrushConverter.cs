using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TestMcAlgorithm.Converters;

public sealed class BusOutputStatusToBrushConverter : IValueConverter
{
    private static readonly Brush OffBrush = CreateBrush("#B7BDC4");
    private static readonly Brush OnBrush = CreateBrush("Lime");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? OnBrush : OffBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static Brush CreateBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
