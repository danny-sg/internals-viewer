using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Query;
using InternalsViewer.Query.Events.Locks;

namespace InternalsViewer.UI.App.ViewModels.Query;

/// <summary>
/// The user-tunable query capture and display options, wrapping the engine's <see cref="EventOptions"/>
/// </summary>
/// <remarks>
/// The engine <see cref="EventOptions"/> is a plain record with no change notification; this view model owns one and
/// exposes observable wrappers so the menu bindings stay live without pushing UI concerns into the engine layer. It
/// raises <see cref="FilterChanged"/> when a change alters which events are shown and <see cref="Changed"/> whenever a
/// persisted option changes.
/// </remarks>
public sealed partial class QueryOptionsViewModel : ObservableObject
{
    private readonly EventOptions _options = new();

    /// <summary>Raised when a change alters which events are shown, so the owner re-applies its filter</summary>
    public event Action? FilterChanged;

    /// <summary>Raised when any persisted option changes, so the owner schedules a save</summary>
    public event Action? Changed;

    /// <summary>The engine options handed to the query runner</summary>
    public EventOptions Options => _options;

    public bool CropToQuery
    {
        get => _options.CropToQuery;
        set => SetOption(_options.CropToQuery, value, v => _options.CropToQuery = v);
    }

    // Off by default: a query's own reads/locks are the point, and system-object noise (metadata, database locks)
    // drowns them out. Drives both capture and the UI-side filter, so a change re-filters the loaded events.
    public bool IncludeSystemObjects
    {
        get => _options.IncludeSystemObjects;
        set => SetOption(_options.IncludeSystemObjects, value, v => _options.IncludeSystemObjects = v, filter: true);
    }

    public bool ShowWaits
    {
        get => _options.IncludeWait;
        set => SetOption(_options.IncludeWait, value, v => _options.IncludeWait = v);
    }

    public bool ShowLatches
    {
        get => _options.IncludeLatch;
        set => SetOption(_options.IncludeLatch, value, v => _options.IncludeLatch = v);
    }

    public bool IncludeMemory
    {
        get => _options.IncludeMemory;
        set => SetOption(_options.IncludeMemory, value, v => _options.IncludeMemory = v);
    }

    public bool IncludeCallStack
    {
        get => _options.IncludeCallStack;
        set => SetOption(_options.IncludeCallStack, value, v => _options.IncludeCallStack = v);
    }

    /// <summary>Whether any lock category is selected, and therefore locks are shown</summary>
    public bool ShowLocks => _options.IncludeLock;

    public bool ShowReadLocks
    {
        get => Includes(LockModeCategory.Read);
        set => SetLockCategory(LockModeCategory.Read, value);
    }

    public bool ShowUpdateLocks
    {
        get => Includes(LockModeCategory.Update);
        set => SetLockCategory(LockModeCategory.Update, value);
    }

    public bool ShowWriteLocks
    {
        get => Includes(LockModeCategory.Write);
        set => SetLockCategory(LockModeCategory.Write, value);
    }

    public bool ShowSchemaLocks
    {
        get => Includes(LockModeCategory.Schema);
        set => SetLockCategory(LockModeCategory.Schema, value);
    }

    public bool ShowRangeLocks
    {
        get => Includes(LockModeCategory.Range);
        set => SetLockCategory(LockModeCategory.Range, value);
    }

    public bool ShowBulkLocks
    {
        get => Includes(LockModeCategory.Bulk);
        set => SetLockCategory(LockModeCategory.Bulk, value);
    }

    /// <summary>Whether the given lock category is selected</summary>
    public bool Includes(LockModeCategory category) => _options.IncludeLockModeCategories.Contains(category);

    /// <summary>
    /// Overwrites the options from a restored layout, raising property notifications but not the change events (the
    /// caller re-filters and the restore itself must not schedule a save)
    /// </summary>
    public void Restore(bool cropToQuery,
                        bool includeSystemObjects,
                        bool includeWait,
                        bool includeLatch,
                        bool includeMemory,
                        bool includeCallStack,
                        IEnumerable<LockModeCategory> lockModeCategories)
    {
        _options.CropToQuery = cropToQuery;
        _options.IncludeSystemObjects = includeSystemObjects;
        _options.IncludeWait = includeWait;
        _options.IncludeLatch = includeLatch;
        _options.IncludeMemory = includeMemory;
        _options.IncludeCallStack = includeCallStack;
        _options.IncludeLockModeCategories = [.. lockModeCategories];

        RaiseAll();
    }

    /// <summary>
    /// Clears every lock category, hiding locks entirely
    /// </summary>
    [RelayCommand]
    private void NoLockModeCategories()
    {
        if (_options.IncludeLockModeCategories.Count == 0)
        {
            return;
        }

        _options.IncludeLockModeCategories.Clear();

        OnLockCategoriesChanged();
    }

    /// <summary>
    /// Resets the lock categories to every category except Schema, sparing the user from re-ticking them all
    /// </summary>
    [RelayCommand]
    private void DefaultLockModeCategories()
    {
        _options.IncludeLockModeCategories = EventOptions.DefaultLockModeCategories();

        OnLockCategoriesChanged();
    }

    private void SetLockCategory(LockModeCategory category, bool selected)
    {
        var changed = selected
            ? _options.IncludeLockModeCategories.Add(category)
            : _options.IncludeLockModeCategories.Remove(category);

        if (!changed)
        {
            return;
        }

        OnLockCategoriesChanged();
    }

    private void OnLockCategoriesChanged()
    {
        RaiseLockCategoryChanges();

        FilterChanged?.Invoke();

        Changed?.Invoke();
    }

    private void SetOption(bool current,
                           bool value,
                           Action<bool> assign,
                           bool filter = false,
                           [CallerMemberName] string? name = null)
    {
        if (current == value)
        {
            return;
        }

        assign(value);

        OnPropertyChanged(name);

        if (filter)
        {
            FilterChanged?.Invoke();
        }

        Changed?.Invoke();
    }

    private void RaiseLockCategoryChanges()
    {
        OnPropertyChanged(nameof(ShowReadLocks));
        OnPropertyChanged(nameof(ShowUpdateLocks));
        OnPropertyChanged(nameof(ShowWriteLocks));
        OnPropertyChanged(nameof(ShowSchemaLocks));
        OnPropertyChanged(nameof(ShowRangeLocks));
        OnPropertyChanged(nameof(ShowBulkLocks));

        OnPropertyChanged(nameof(ShowLocks));
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(CropToQuery));
        OnPropertyChanged(nameof(IncludeSystemObjects));
        OnPropertyChanged(nameof(ShowWaits));
        OnPropertyChanged(nameof(ShowLatches));
        OnPropertyChanged(nameof(IncludeMemory));
        OnPropertyChanged(nameof(IncludeCallStack));

        RaiseLockCategoryChanges();
    }
}
