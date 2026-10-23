using System.ComponentModel;
using InternalsViewer.UI.App.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace InternalsViewer.UI.App.Controls.Query.Trace.CallStack;

public sealed partial class DecodeToggle : ToggleButton
{
    public DecodeToggle()
    {
        DefaultStyleKey = typeof(ToggleButton);

        Style = (Style)Application.Current.Resources["TabCommandToggleStyle"];

        Content = new TextBlock { Text = "Decode", VerticalAlignment = VerticalAlignment.Center };

        IsChecked = Settings.DecodeValues;

        ToolTipService.SetToolTip(this,
                                  "Show each value as what it stands for - a table and index, an operator, a page or a name - rather "
                                  + "than as hex. A pointer to another object is shown with its class.");

        Click += (_, _) => Settings.DecodeValues = IsChecked == true;

        Loaded += (_, _) =>
        {
            IsChecked = Settings.DecodeValues;

            Settings.PropertyChanged += OnSettingsPropertyChanged;
        };

        Unloaded += (_, _) => Settings.PropertyChanged -= OnSettingsPropertyChanged;
    }

    private SettingsViewModel Settings { get; } = App.GetService<SettingsViewModel>();

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.DecodeValues))
        {
            IsChecked = Settings.DecodeValues;
        }
    }
}
