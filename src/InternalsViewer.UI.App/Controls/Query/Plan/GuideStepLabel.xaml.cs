using System;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Plan;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Query.Plan;

public sealed partial class GuideStepLabel : UserControl
{
    public GuideStepLabel()
    {
        InitializeComponent();
    }

    public event EventHandler<bool>? Clicked;

    public void Show(GuideStepItem step)
    {
        NameText.Config = step.Config;

        NameText.Text = step.Name;

        DescriptionText.Config = step.Config;

        DescriptionText.Text = step.Description;

        DescriptionText.Visibility = step.HasDescription ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnClick(object sender, RoutedEventArgs e) => Clicked?.Invoke(this, KeyboardState.IsShiftDown());
}
