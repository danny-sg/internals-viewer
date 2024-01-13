namespace InternalsViewer.UI.App.vNext.Controls.Page;
public sealed partial class LabelTextBox
{
    public LabelTextBox()
    {
        InitializeComponent();

        DataContext = this;
    }

    public string Label
    {
        get { return (string)GetValue(LabelProperty); }
        set { SetValue(LabelProperty, value); }
    }

    public static readonly DependencyProperty LabelProperty = DependencyProperty
        .Register(nameof(Label),
            typeof(string),
            typeof(LabelTextBox),
            PropertyMetadata.Create(() => "Label"));

    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty
        .Register(nameof(Text),
            typeof(string),
            typeof(LabelTextBox),
            PropertyMetadata.Create(() => "TextBox"));

    public double LabelWidth
    {
        get { return (double)GetValue(LabelWidthProperty); }
        set { SetValue(LabelWidthProperty, value); }
    }

    public static readonly DependencyProperty LabelWidthProperty = DependencyProperty
        .Register(nameof(LabelWidth),
            typeof(double),
            typeof(LabelTextBox),
            PropertyMetadata.Create(() => 100));
}
