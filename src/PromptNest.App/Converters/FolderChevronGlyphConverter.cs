using Microsoft.UI.Xaml.Data;

namespace PromptNest.App.Converters;

public sealed class FolderChevronGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        return value is true ? "\uE70D" : "\uE76C";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(targetType);

        throw new NotSupportedException();
    }
}