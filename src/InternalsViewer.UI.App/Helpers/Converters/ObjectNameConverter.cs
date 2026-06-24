using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

public class ObjectNameConverter : IValueConverter
{
    public Dictionary<int, string> Names { get; set; } = [];

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not int objectId || objectId == 0)
        {
            return string.Empty;
        }

        return Names.TryGetValue(objectId, out var name) ? name : $"(Object Id: {objectId})";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
