using Microsoft.UI.Xaml.Data;
using System;

namespace FangJia.Converters
{
    internal class BoolToMenuTextConverter : IValueConverter
    {
        public string? TrueText { get; set; }
        public string? FalseText { get; set; }
        public object? Convert(object? value, Type targetType, object parameter, string language)
        {
            return value is true ? TrueText : FalseText;
        }

        public object ConvertBack(object? value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
