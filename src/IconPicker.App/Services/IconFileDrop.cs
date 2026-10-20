using System.Runtime.InteropServices;
using IconPicker.Core.Candidates;
using IconPicker.Core.Shell;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace IconPicker.App.Services;

/// <summary>A part of the window that takes files dropped from Explorer (or Everything) as icons.</summary>
internal interface IIconFileDropTarget
{
    /// <summary>Whether a drop on <paramref name="element"/>, which lies inside this one, is taken here.</summary>
    bool TakesDropOn(DependencyObject element);

    /// <summary>Takes the dropped files: each one an icon, a picture, an SVG or a shortcut showing an icon.</summary>
    Task DropAsync(DependencyObject element, IReadOnlyList<string> iconFiles);
}

/// <summary>
/// Files dragged onto the window: icon files go to the part under the pointer that takes them (<see cref="IIconFileDropTarget"/>), and a folder opens anywhere else.
/// One handler on the window (and one on a dialog) decides for every part, so the parts never compete for a drag.
/// </summary>
internal static class IconFileDrop
{
    private static DataPackageView? _lastDataView;
    private static DraggedItems _lastItems = DraggedItems.None;

    /// <summary>For DragOver on <paramref name="root"/>: shows what dropping here would do. <paramref name="opensFolders"/> lets a dragged folder open here (the window, not a dialog).</summary>
    public static async Task ShowDragAsync(UIElement root, DragEventArgs eventArgs, bool opensFolders)
    {
        var element = FindElementUnderPointer(root, eventArgs);
        var deferral = eventArgs.GetDeferral();
        try
        {
            var items = await ReadAsync(eventArgs.DataView);
            if (items.IconFiles.Count > 0 && FindTarget(element) is not null)
            {
                eventArgs.AcceptedOperation = DataPackageOperation.Copy;
                eventArgs.DragUIOverride.Caption = Strings.Get("UseAsIconCaption");
            }
            else if (opensFolders && items.FolderPath is not null)
            {
                eventArgs.AcceptedOperation = DataPackageOperation.Link;
                eventArgs.DragUIOverride.Caption = Strings.Get("DropCaption");
            }
            else
            {
                eventArgs.AcceptedOperation = DataPackageOperation.None;
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    /// <summary>For Drop on <paramref name="root"/>: hands the icon files to the part under the pointer. Returns the dragged folder when nothing took icon files, for the window to open.</summary>
    public static async Task<string?> DropAsync(UIElement root, DragEventArgs eventArgs)
    {
        var element = FindElementUnderPointer(root, eventArgs);
        DraggedItems items;
        // Released before the files are used: the app the files came from waits until then.
        var deferral = eventArgs.GetDeferral();
        try
        {
            items = await ReadAsync(eventArgs.DataView);
        }
        finally
        {
            deferral.Complete();
        }
        if (items.IconFiles.Count > 0 && element is not null && FindTarget(element) is { } target)
        {
            await target.DropAsync(element, items.IconFiles);
            return null;
        }
        return items.FolderPath;
    }

    /// <summary>Whether <paramref name="element"/> is <paramref name="container"/> or lies inside it.</summary>
    public static bool IsInside(DependencyObject? element, DependencyObject container)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, container))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The innermost element under the pointer: the event's source, or, when that is the handler's own element, the topmost element found there.</summary>
    private static DependencyObject? FindElementUnderPointer(UIElement root, DragEventArgs eventArgs)
    {
        if (eventArgs.OriginalSource is DependencyObject source && !ReferenceEquals(source, root))
        {
            return source;
        }
        return VisualTreeHelper.FindElementsInHostCoordinates(eventArgs.GetPosition(null), root).FirstOrDefault() ?? root;
    }

    /// <summary>The nearest part, at or around <paramref name="element"/>, that takes a drop there.</summary>
    private static IIconFileDropTarget? FindTarget(DependencyObject? element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is IIconFileDropTarget target && target.TakesDropOn(element!))
            {
                return target;
            }
        }
        return null;
    }

    /// <summary>The drag's files, read once per drag: DragOver repeats with every move of the pointer.</summary>
    private static async Task<DraggedItems> ReadAsync(DataPackageView dataView)
    {
        if (ReferenceEquals(dataView, _lastDataView))
        {
            return _lastItems;
        }
        var items = DraggedItems.None;
        if (dataView.Contains(StandardDataFormats.StorageItems))
        {
            try
            {
                var paths = (await dataView.GetStorageItemsAsync()).Select(item => item.Path).Where(path => !string.IsNullOrEmpty(path)).ToList();
                items = new DraggedItems(paths.FirstOrDefault(IsFolder), paths.Where(IsIconFile).ToList());
            }
            catch (COMException)
            {
                // The app the drag came from didn't hand its files over.
            }
        }
        _lastDataView = dataView;
        _lastItems = items;
        return items;
    }

    /// <summary>A folder, or a shortcut to one, which opens wherever it is dropped.</summary>
    private static bool IsFolder(string path)
    {
        return Directory.Exists(path) || ShortcutReader.ReadFolderTarget(path) is not null;
    }

    /// <summary>A file Browse could pick, or a shortcut that isn't to a folder (its icon is used).</summary>
    private static bool IsIconFile(string path)
    {
        return File.Exists(path) && (IconSource.GetKind(path) is not null || (ShortcutReader.IsShortcut(path) && ShortcutReader.ReadFolderTarget(path) is null));
    }

    private sealed record DraggedItems(string? FolderPath, IReadOnlyList<string> IconFiles)
    {
        public static DraggedItems None { get; } = new(null, []);
    }
}
