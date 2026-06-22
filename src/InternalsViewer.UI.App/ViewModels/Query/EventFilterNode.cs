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
    private bool _suppressNotify;

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

            if (!_suppressNotify)
            {
                OnPropertyChanged();
            }

            if (value is not null)
            {
                foreach (var child in Children)
                {
                    child._suppressNotify = true;
                    child.IsChecked = value;
                    child._suppressNotify = false;
                }

                // Fire once for each child after all are updated
                foreach (var child in Children)
                {
                    child.OnPropertyChanged(nameof(IsChecked));
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

        var newValue = allChecked ? true : noneChecked ? (bool?)false : null;

        if (_isChecked == newValue)
        {
            return;
        }

        _isChecked = newValue;
        OnPropertyChanged(nameof(IsChecked));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
