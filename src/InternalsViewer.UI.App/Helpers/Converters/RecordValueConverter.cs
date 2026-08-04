using System;
using System.Linq;
using InternalsViewer.UI.App.Models.Index;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.Helpers.Converters;

internal sealed class RecordValueConverter: IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not IndexRecordModel record)
        {
            return string.Empty;
        }

        if (parameter is int index)
        {
            return index >= 0 && index < record.Fields.Count ? record.Fields[index].Value : string.Empty;
        }

        var field = record.Fields.FirstOrDefault(f => f.Name == parameter.ToString());

        return field?.Value ?? $"{parameter} not found";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}