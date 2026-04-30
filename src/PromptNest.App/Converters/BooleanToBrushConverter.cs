using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace PromptNest.App.Converters;

public sealed class BooleanToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        if (value is true && parameter is "SelectedRow")
        {
            return Application.Current.Resources["PromptNestSelectedRowBrush"];
        }

        return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(targetType);

        throw new NotSupportedException();
    }
}