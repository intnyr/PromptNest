using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PromptNest.App.Converters;

public sealed class DepthToPaddingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        double indent = value is double pixels ? pixels : 0;
        return new Thickness(indent, 0, 0, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(targetType);

        throw new NotSupportedException();
    }
}