using DevExpress.Xpf.Editors;
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

public sealed class ValueToBrushConverter : IValueConverter
{
    private static readonly Brush OffBrush = CreateBrush("#FFF68E36");  // ornage
    private static readonly Brush OnBrush = CreateBrush("#FF36C0F6");   // blue

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

public sealed class ConvertFunction
{
    public byte[] RegistersToBytes(IReadOnlyList<ushort> registers, bool wordSwap = false)
    {
        if (registers == null)
            throw new ArgumentNullException(nameof(registers));

        if (wordSwap && registers.Count % 2 != 0)
            throw new ArgumentException("wordSwap requires an even number of registers.");

        byte[] bytes = new byte[registers.Count * 2];

        for (int i = 0; i < registers.Count; i++)
        {
            int srcIndex = wordSwap ? (i ^ 1) : i;
            ushort value = registers[srcIndex];

            bytes[i * 2] = (byte)(value & 0xFF);
            bytes[i * 2 + 1] = (byte)(value >> 8);
        }

        return bytes;
    }
}
