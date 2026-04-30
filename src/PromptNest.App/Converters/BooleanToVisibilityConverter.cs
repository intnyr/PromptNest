using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PromptNest.App.Converters;

public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        bool isVisible = value is true;
        if (parameter is "Invert")
        {
            isVisible = !isVisible;
        }

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(targetType);

        throw new NotSupportedException();
    }
}