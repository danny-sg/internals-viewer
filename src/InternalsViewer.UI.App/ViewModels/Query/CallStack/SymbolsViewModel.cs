using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.CallStack.Symbols;
using InternalsViewer.UI.App.Models.Query.CallStack;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public sealed partial class SymbolsViewModel(ILogger logger, SettingsViewModel settings) : ObservableObject, IDisposable
{
    private const int SymbolSearchMinimumLength = 3;

    private const int SymbolSearchLimit = 200;

    private const int ExpandedGroupLimit = 10;

    private const char ModuleSeparator = ';';

    private static readonly TimeSpan SymbolSearchDelay = TimeSpan.FromMilliseconds(300);

    private static readonly Lazy<IReadOnlyList<SymbolSearchSuggestion>> Suggestions =
        new(() => SymbolSearchSuggestion.FromMappings(CategoryMappings.Default));

    private readonly List<(CallstackFrame Frame, string ClassName)> _membersHistory = [];

    [ObservableProperty]
    private bool _isPaneVisible;

    [ObservableProperty]
    private bool _isPaneDockedBottom;

    [ObservableProperty]
    private bool _isSymbolSearchSelected;

    [ObservableProperty]
    private bool _isMembersLoading;

    [ObservableProperty]
    private string _membersClassName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MembersSummary))]
    [NotifyPropertyChangedFor(nameof(FilteredMembers))]
    private ClassMemberListing? _members;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredMembers))]
    private string _membersFilter = string.Empty;

    [ObservableProperty]
    private string? _membersMessage;

    [ObservableProperty]
    private bool _canGoBackMembers;

    [ObservableProperty]
    private bool _symbolSearchModule;

    [ObservableProperty]
    private bool _symbolSearchClass;

    [ObservableProperty]
    private bool _symbolSearchSignature;

    [ObservableProperty]
    private string _symbolSearchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SymbolSearchItems))]
    private IReadOnlyList<SymbolModuleRow> _symbolSearchModules = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExcludedModules))]
    [NotifyPropertyChangedFor(nameof(SymbolSearchItems))]
    private IReadOnlyList<string> _excludedModules = [];

    [ObservableProperty]
    private string? _symbolSearchMessage;

    [ObservableProperty]
    private bool _isSymbolSearchLoading;

    private CallstackResolver? _symbolResolver;

    private string? _symbolResolverPath;

    private int _membersRequest;

    private int _symbolSearchRequest;

    private (CallstackFrame Frame, string ClassName)? _currentMembers;

    /// <summary>
    /// The query's call stacks, whose frames say which modules and symbol files to read
    /// </summary>
    public CallStackTree? CallStack { get; set; }

    public bool HasExcludedModules => ExcludedModules.Count > 0;

    /// <summary>
    /// The result tree's top level: the modules found, then a row offering to clear the exclusions while any hold
    /// </summary>
    public IReadOnlyList<object> SymbolSearchItems =>
        HasExcludedModules
            ? [.. SymbolSearchModules, new SymbolExclusionsRow(ExcludedModules)]
            : [.. SymbolSearchModules];

    public string MembersSummary
    {
        get
        {
            if (Members is not { Groups.Count: > 0 } listing)
            {
                return string.Empty;
            }

            var summary = $"{listing.Count} {(listing.Count == 1 ? "member" : "members")}";

            return listing.Groups.Count == 1
                ? summary
                : $"{summary}, {string.Join(", ", listing.Groups.Select(g => $"{g.Module} {g.Members.Count}"))}";
        }
    }

    public IReadOnlyList<ClassMemberRow> FilteredMembers
    {
        get
        {
            if (Members is not { } listing)
            {
                return [];
            }

            var showModule = listing.Groups.Count > 1;

            var overloaded = listing.Groups
                                    .SelectMany(g => g.Members)
                                    .GroupBy(m => (m.Module, m.Name))
                                    .Where(g => g.Count() > 1)
                                    .Select(g => g.Key)
                                    .ToHashSet();

            return listing.Groups
                          .SelectMany(g => g.Members)
                          .Where(m => string.IsNullOrWhiteSpace(MembersFilter)
                                      || m.Signature.Contains(MembersFilter, StringComparison.OrdinalIgnoreCase))
                          .Select(m => new ClassMemberRow(showModule ? $"{m.Module}!" : string.Empty,
                                                          m,
                                                          listing.ClassName,
                                                          overloaded.Contains((m.Module, m.Name)))
                                       {
                                           Highlight = MembersFilter
                                       })
                          .ToList();
        }
    }

    private SymbolSearchFields SymbolSearchFields =>
        (SymbolSearchModule ? SymbolSearchFields.Module : SymbolSearchFields.None)
        | (SymbolSearchClass ? SymbolSearchFields.Class : SymbolSearchFields.None)
        | (SymbolSearchSignature ? SymbolSearchFields.Signature : SymbolSearchFields.None);

    public IReadOnlyList<SymbolSearchSuggestion> SuggestionsFor(string text) =>
        Suggestions.Value.Where(s => s.Matches(text)).ToList();

    /// <summary>
    /// Shows the pane on the symbol search
    /// </summary>
    public void ShowSymbolSearch()
    {
        IsPaneVisible = true;
        IsSymbolSearchSelected = true;
    }

    /// <summary>
    /// Leaves a module out of every symbol search from now on
    /// </summary>
    public void ExcludeModule(string module)
    {
        if (ExcludedModules.Contains(module, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        SaveExcludedModules(ExcludedModules.Append(module).OrderBy(m => m, StringComparer.OrdinalIgnoreCase).ToList());
    }

    /// <summary>
    /// Searches every module again
    /// </summary>
    public void ClearModuleExclusions() => SaveExcludedModules([]);

    /// <summary>
    /// Reads the excluded modules from Settings, on first use and whenever Settings change underneath
    /// </summary>
    public void LoadExcludedModules()
    {
        var modules = settings.SymbolSearchExcludedModules
                              .Split(ModuleSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                              .ToList();

        if (!modules.SequenceEqual(ExcludedModules, StringComparer.OrdinalIgnoreCase))
        {
            ExcludedModules = modules;
        }
    }

    /// <summary>
    /// Lists the members of a call stack frame's class in the Members pane
    /// </summary>
    public Task ListMembersAsync(CallStackNode node)
    {
        if (node.Frame is { Resolved.ClassName: { Length: > 0 } className } frame)
        {
            return NavigateMembersAsync(frame, className);
        }

        IsPaneVisible = true;
        IsSymbolSearchSelected = false;

        _membersRequest++;

        _currentMembers = null;

        Members = null;
        MembersFilter = string.Empty;
        MembersClassName = node.Symbol;
        MembersMessage = "The frame is not a class member";

        return Task.CompletedTask;
    }

    /// <summary>
    /// Lists the members of a type named in the current listing, from the same module's symbols
    /// </summary>
    public Task ListMembersAsync(string className)
        => _currentMembers is { } current ? NavigateMembersAsync(current.Frame, className) : Task.CompletedTask;

    /// <summary>
    /// Returns the Members pane to the listing shown before the last navigation
    /// </summary>
    public Task GoBackMembersAsync()
    {
        if (_membersHistory.Count == 0)
        {
            return Task.CompletedTask;
        }

        var (frame, className) = _membersHistory[^1];

        _membersHistory.RemoveAt(_membersHistory.Count - 1);

        CanGoBackMembers = _membersHistory.Count > 0;

        return ListMembersAsync(frame, className);
    }

    /// <summary>
    /// Resolves the signature of the function a call stack frame is in, off the UI thread
    /// </summary>
    /// <remarks>
    /// Best effort: a signature is a nicety over the register dump, so a resolver failure returns null and lets the
    /// caller fall back rather than failing the command.
    /// </remarks>
    public async Task<string?> ResolveFrameSignatureAsync(CallstackFrame frame)
    {
        try
        {
            return await Task.Run(() => GetSymbolResolver().ResolveSignature(frame));
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Resolving the signature for a frame failed");

            return null;
        }
    }

    public void Dispose()
    {
        _symbolResolver?.Dispose();
        _symbolResolver = null;
    }

    partial void OnSymbolSearchTextChanged(string value) => _ = SearchSymbolsAsync(value);

    partial void OnSymbolSearchModuleChanged(bool value) => _ = SearchSymbolsAsync(SymbolSearchText);

    partial void OnSymbolSearchClassChanged(bool value) => _ = SearchSymbolsAsync(SymbolSearchText);

    partial void OnSymbolSearchSignatureChanged(bool value) => _ = SearchSymbolsAsync(SymbolSearchText);

    partial void OnExcludedModulesChanged(IReadOnlyList<string> value) => _ = SearchSymbolsAsync(SymbolSearchText);

    private void SaveExcludedModules(IReadOnlyList<string> modules)
    {
        settings.SymbolSearchExcludedModules = string.Join(ModuleSeparator, modules);

        ExcludedModules = modules;
    }

    private Task NavigateMembersAsync(CallstackFrame frame, string className)
    {
        if (_currentMembers is { } current && current != (frame, className))
        {
            _membersHistory.Add(current);

            CanGoBackMembers = true;
        }

        return ListMembersAsync(frame, className);
    }

    private async Task ListMembersAsync(CallstackFrame frame, string className)
    {
        IsPaneVisible = true;
        IsSymbolSearchSelected = false;

        var request = ++_membersRequest;

        _currentMembers = (frame, className);

        Members = null;
        MembersFilter = string.Empty;
        MembersClassName = $"{frame.Module}!{className}";
        MembersMessage = null;
        IsMembersLoading = true;

        try
        {
            var resolver = GetSymbolResolver();

            var listing = await resolver.ListMembersAsync(MemberSearchFrames(frame), className);

            if (request != _membersRequest)
            {
                return;
            }

            Members = listing;
            MembersMessage = listing.Count == 0 ? $"No symbols found for {className}" : null;

            if (listing.Groups.Count > 0)
            {
                MembersClassName = $"{listing.Groups[0].Module}!{className}";
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Listing members for {ClassName} failed", className);

            if (request == _membersRequest)
            {
                MembersMessage = $"Listing {className} members failed: {exception.Message}";
            }
        }
        finally
        {
            if (request == _membersRequest)
            {
                IsMembersLoading = false;
            }
        }
    }

    private async Task SearchSymbolsAsync(string text)
    {
        var request = ++_symbolSearchRequest;

        var query = text.Trim();

        if (query.Length < SymbolSearchMinimumLength)
        {
            SymbolSearchModules = [];
            SymbolSearchMessage = query.Length == 0 ? null : $"Type at least {SymbolSearchMinimumLength} characters";
            IsSymbolSearchLoading = false;

            return;
        }

        await Task.Delay(SymbolSearchDelay);

        if (request != _symbolSearchRequest)
        {
            return;
        }

        LoadExcludedModules();

        var frames = ModuleFrames();

        if (frames.Count == 0)
        {
            SymbolSearchModules = [];
            SymbolSearchMessage = HasExcludedModules
                ? "Every module the query's call stacks came from is excluded"
                : "Run a query with Call Stack events first, so the modules to search are known";

            return;
        }

        IsSymbolSearchLoading = true;
        SymbolSearchMessage = null;

        try
        {
            var matches = await GetSymbolResolver().SearchSymbolsAsync(frames, query, SymbolSearchFields, SymbolSearchLimit);

            if (request != _symbolSearchRequest)
            {
                return;
            }

            SymbolSearchModules = SymbolModules(matches, query);

            SymbolSearchMessage = matches.Count == 0 ? $"No symbols match {query}" : null;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Searching symbols for {Text} failed", query);

            if (request == _symbolSearchRequest)
            {
                SymbolSearchMessage = $"Searching symbols failed: {exception.Message}";
            }
        }
        finally
        {
            if (request == _symbolSearchRequest)
            {
                IsSymbolSearchLoading = false;
            }
        }
    }

    /// <summary>
    /// The matches as a module per node, a class per node beneath, and the members under each class
    /// </summary>
    /// <remarks>
    /// Modules start expanded. Classes start expanded only when there are few enough to take in at once. A member
    /// that is exactly what was searched for is not highlighted, as colouring all of it says nothing.
    /// </remarks>
    private static IReadOnlyList<SymbolModuleRow> SymbolModules(IReadOnlyList<SymbolMatch> matches, string query)
    {
        var highlight = query;

        var overloaded = matches.GroupBy(m => (m.Member.Module, m.ClassName, m.Member.Name))
                                .Where(g => g.Count() > 1)
                                .Select(g => g.Key)
                                .ToHashSet();

        var groups = matches.GroupBy(m => (m.Member.Module, m.ClassName))
                            .OrderBy(g => g.Key.Module, StringComparer.Ordinal)
                            .ThenBy(g => g.Key.ClassName, StringComparer.Ordinal)
                            .Select(g => new SymbolGroupRow(g.Key.Module,
                                                            g.Key.ClassName,
                                                            g.OrderBy(m => m.Member.Signature, StringComparer.Ordinal)
                                                             .Select(m => SymbolRow(m, highlight, overloaded))
                                                             .ToList(),
                                                            highlight)
                                         {
                                             IsExpanded = false
                                         })
                            .ToList();

        foreach (var group in groups)
        {
            group.IsExpanded = groups.Count <= ExpandedGroupLimit;
        }

        return groups.GroupBy(g => g.Module)
                     .Select(m => new SymbolModuleRow(m.Key, m.ToList()))
                     .ToList();
    }

    private static ClassMemberRow SymbolRow(SymbolMatch match,
                                            string highlight,
                                            HashSet<(string Module, string ClassName, string Name)> overloaded)
    {
        var fullName = match.ClassName.Length > 0 ? $"{match.ClassName}::{match.Member.Name}" : match.Member.Name;

        var exact = SearchHighlightRanges.Alternatives(highlight)
                                         .Select(SearchHighlightRanges.SymbolPart)
                                         .Any(s => string.Equals(fullName, s, StringComparison.OrdinalIgnoreCase));

        return new ClassMemberRow(string.Empty,
                                  match.Member,
                                  match.ClassName,
                                  overloaded.Contains((match.Member.Module, match.ClassName, match.Member.Name)))
        {
            Highlight = exact ? string.Empty : highlight
        };
    }

    /// <summary>
    /// The frame's own module first, then every other module the query's call stacks carry symbols for
    /// </summary>
    private List<CallstackFrame> MemberSearchFrames(CallstackFrame frame)
    {
        var frames = new List<CallstackFrame> { frame };

        AddModuleFrames(frames);

        return frames;
    }

    /// <summary>
    /// One frame per module the query's call stacks carry symbols for
    /// </summary>
    private List<CallstackFrame> ModuleFrames()
    {
        var frames = new List<CallstackFrame>();

        AddModuleFrames(frames);

        return frames;
    }

    private void AddModuleFrames(List<CallstackFrame> frames)
    {
        foreach (var node in CallStack?.Nodes() ?? [])
        {
            if (node.Frame is { Pdb.Length: > 0 } candidate
                && !ExcludedModules.Contains(candidate.Module, StringComparer.OrdinalIgnoreCase)
                && !frames.Any(f => f.Pdb == candidate.Pdb && f.Guid == candidate.Guid && f.Age == candidate.Age))
            {
                frames.Add(candidate);
            }
        }
    }

    private CallstackResolver GetSymbolResolver()
    {
        var symbolsPath = settings.SymbolsPath;

        if (_symbolResolver is null || _symbolResolverPath != symbolsPath)
        {
            _symbolResolver?.Dispose();

            _symbolResolver = new CallstackResolver(symbolsPath);
            _symbolResolverPath = symbolsPath;
        }

        return _symbolResolver;
    }
}
