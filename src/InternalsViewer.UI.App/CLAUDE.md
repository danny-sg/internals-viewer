# InternalsViewer.UI.App

Notes specific to the WinUI 3 app. The root [CLAUDE.md](../../CLAUDE.md) covers build commands and the
overall architecture.

## Layout

MVVM with CommunityToolkit.Mvvm: `Views/` (XAML) → `ViewModels/` → `Models/`. `Controls/` holds the
custom SkiaSharp-drawn surfaces (timeline, plan, allocation map, columnstore structure) — these draw into
an `SKCanvas` and do their own hit testing rather than composing XAML elements.

Documents are dockable tabs. Test folders in `InternalsViewer.UI.App.Tests` mirror this structure exactly
(`Controls\Timeline\Renderers`, etc.), unlike the flat test projects elsewhere in the solution.

## Grids — WinUI.TableView

Grids use `WinUI.TableView` (1.4.1)

- **Every `TableView` needs `ListViewItemMinHeight="0"`** or `RowHeight` is silently ignored.
- Row load/unload hooks off `ContainerContentChanging`; cell recycling off `RefreshElement`.
- `Sorting` has a `Handled` contract — set it when you sort yourself, or the control sorts again.
- Cell population via `Binding` costs roughly 3x what `DataContextChanged` does. On wide grids that is the
  difference between a tab switching instantly and visibly hitching.

## TabView

- `TabView` does **not** stretch vertically by default. In a star Grid row it sizes to content, which is
  near-zero when the child is a SkiaSharp canvas. Set `VerticalAlignment="Stretch"` explicitly.
- With `TabItemsSource` it never auto-selects a tab — guard `SelectedIndex` on `Loaded` and
  `SelectionChanged`.
- Tab content is rebuilt on every switch. Where that is too slow the pattern is a tab strip plus panels
  toggled by `Visibility`, rather than real tab items.

## Disposal

Closing a tab must dispose its view model chain or the whole connection leaks. The entry point is the
`Tab.Content is IDisposable` check; the view owns its view model and disposes down the chain.

Query documents are **reused** after Close, so they must not be disposed there. Columnstore documents
dispose via the `DocumentClosed` event plus a `Dispose` override that walks the dock.

SkiaSharp resoures are unmanaged so care must be taken to dispose correctly.

## XAML gotchas

- CommunityToolkit XAML namespaces differ between the old `.UI` packages and the current ones — for example
  `GridSplitter` is in `CommunityToolkit.WinUI.Controls`, with no `.UI`. Check the prefix per control.
- `x:Bind` cannot bind a property with an `init`-only accessor. Model records used from XAML need settable
  properties.
- WebView2 virtual host names ending in `.local` cost a multi-second mDNS lookup per navigation. Use
  `.localhost`.

## Display strings

UI labels are Title Case — "Bit Pack Entries", not "Bit pack entries". Never use "..." — write "etc.".
