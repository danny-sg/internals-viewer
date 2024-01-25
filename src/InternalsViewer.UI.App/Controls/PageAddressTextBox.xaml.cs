using System;
using Windows.ApplicationModel.DataTransfer;
using InternalsViewer.Internals.Engine.Address;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.Internals.Engine.Parsers;
using Windows.System;

namespace InternalsViewer.UI.App.Controls
{
    public sealed partial class PageAddressTextBox
    {
        public event EventHandler<PageNavigationEventArgs>? AddressChanged;

        public PageAddress? PageAddress
        {
            get => (PageAddress?)GetValue(PageAddressProperty);
            set => SetValue(PageAddressProperty, value);
        }

        public static readonly DependencyProperty PageAddressProperty
            = DependencyProperty.Register(nameof(PageAddress),
                typeof(PageAddress?),
                typeof(PageAddressTextBox),
                new PropertyMetadata(default, null));

        public string DatabaseName  
        {
            get => (string)GetValue(DatabaseNameProperty);
            set => SetValue(DatabaseNameProperty, value);
        }

        public static readonly DependencyProperty DatabaseNameProperty
            = DependencyProperty.Register(nameof(DatabaseName),
                typeof(string),
                typeof(PageAddressTextBox),
                new PropertyMetadata(default, null));

        public PageAddressTextBox()
        {
            InitializeComponent();
        }

        private void TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                if (PageAddressParser.TryParse(TextBox.Text, out var pageAddress))
                {
                    AddressChanged?.Invoke(this, new PageNavigationEventArgs(pageAddress.FileId, pageAddress.PageId));
                }
            }
        }

        private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            PageAddressParser.TryParse(TextBox.Text, out var pageAddress);

            var option = int.Parse((sender as MenuFlyoutItem)?.Tag.ToString() ?? string.Empty);

            var text = $"DBCC TRACEON (3604);{Environment.NewLine}" +
                       $"DBCC PAGE('{DatabaseName}', " +
                       $"{pageAddress.FileId}, " +
                       $"{pageAddress.PageId}, " +
                       $"{option});";

            var package = new DataPackage();
            
            package.SetText(text);

            Clipboard.SetContent(package);
        }
    }
}
