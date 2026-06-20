using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed class EventFilterNode : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Label { get; set; } = string.Empty;

    private bool? _isChecked = true;

    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
            {
                return;
            }

            _isChecked = value;

            OnPropertyChanged();

            if (value is not null)
            {
                foreach (var child in Children)
                {
                    child.IsChecked = value;
                }
            }

            Parent?.RefreshCheckedState();
        }
    }

    public ObservableCollection<EventFilterNode> Children { get; } = [];

    public bool IsLeaf => Children.Count == 0;

    internal EventFilterNode? Parent { get; set; }

    internal void RefreshCheckedState()
    {
        var allChecked  = Children.All(c => c.IsChecked == true);
        var noneChecked = Children.All(c => c.IsChecked == false);

        _isChecked = allChecked ? true : noneChecked ? false : null;
        OnPropertyChanged(nameof(IsChecked));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
