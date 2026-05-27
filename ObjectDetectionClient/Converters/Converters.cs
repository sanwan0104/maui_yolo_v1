using System.Globalization;
using ObjectDetectionClient.Models;

namespace ObjectDetectionClient.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status?.ToLower() switch
            {
                "pending" => Color.FromArgb("#FF9800"),    // 橙色
                "processing" => Color.FromArgb("#2196F3"), // 蓝色
                "completed" => Color.FromArgb("#4CAF50"),  // 绿色
                "failed" => Color.FromArgb("#F44336"),     // 红色
                _ => Colors.Gray
            };
        }
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BboxConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DetectionResult result)
        {
            return $"({result.BboxX1}, {result.BboxY1}) - ({result.BboxX2}, {result.BboxY2})";
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
