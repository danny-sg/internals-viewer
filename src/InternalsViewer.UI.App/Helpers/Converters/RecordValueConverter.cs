using Microsoft.UI.Xaml.Data;
using System;
using System.Linq;
using InternalsViewer.UI.App.Models.Index;

namespace InternalsViewer.UI.App.Helpers.Converters;

internal class RecordValueConverter: IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not IndexRecordModel record)
        {
            return string.Empty;
        }

        var field = record.Fields.FirstOrDefault(f => f.Name == parameter.ToString());

        return field?.Value ?? $"{ parameter} not found";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
