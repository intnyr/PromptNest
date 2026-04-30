using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

using PromptNest.App.ViewModels;

namespace PromptNest.App.Converters;

public sealed class ValidationStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        string key = value is ValidationState.Failed
            ? "PromptNestDangerBrush"
            : "PromptNestSuccessBrush";

        return Application.Current.Resources[key];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(targetType);

        throw new NotSupportedException();
    }
}