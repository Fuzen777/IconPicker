using System.Runtime.InteropServices;
using IconPicker.Core.Candidates;
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
    /// <summary>The drag whose files were read, or are being read.</summary>
    private static DataPackageView? _readDataView;

    /// <summary>Its files, or null while they are being read.</summary>
    private static IconFileSelection? _readItems;

    /// <summary>
    /// For DragOver on <paramref name="root"/>: shows what dropping here would do. <paramref name="opensFolders"/> lets a dragged folder open here (the window, not a dialog).
    /// Windows takes the answer only when given at once, so the drag's files are read in the background on its first DragOver: until they are, a drag over a part that takes icon files counts as icon files, and anywhere else as a folder.
    /// DragOver repeats as the pointer moves, so the answer is exact a moment later.
    /// </summary>
    public static void ShowDrag(UIElement root, DragEventArgs eventArgs, bool opensFolders)
    {
        if (!eventArgs.DataView.Contains(StandardDataFormats.StorageItems))
        {
            eventArgs.AcceptedOperation = DataPackageOperation.None;
            return;
        }
        var items = GetItemsOrStartReading(eventArgs.DataView);
        var usesIconFiles = (items is null || items.IconFiles.Count > 0) && FindTargetUnderPointer(root, eventArgs) is not null;
        if (usesIconFiles)
        {
            eventArgs.AcceptedOperation = DataPackageOperation.Copy;
            eventArgs.DragUIOverride.Caption = Strings.Get("UseAsIconCaption");
        }
        else if (opensFolders && (items is null || items.FolderPath is not null))
        {
            eventArgs.AcceptedOperation = DataPackageOperation.Link;
            eventArgs.DragUIOverride.Caption = Strings.Get("DropCaption");
        }
        else
        {
            eventArgs.AcceptedOperation = DataPackageOperation.None;
        }
    }

    /// <summary>For Drop on <paramref name="root"/>: hands the icon files to the part under the pointer. Returns the dragged folder when nothing took icon files, for the window to open.</summary>
    public static async Task<string?> DropAsync(UIElement root, DragEventArgs eventArgs)
    {
        var targetUnderPointer = FindTargetUnderPointer(root, eventArgs);
        var dataView = eventArgs.DataView;
        IconFileSelection items;
        // Released before the files are used: the app the files came from waits until then.
        var deferral = eventArgs.GetDeferral();
        try
        {
            items = (ReferenceEquals(dataView, _readDataView) ? _readItems : null) ?? await ReadAsync(dataView);
        }
        finally
        {
            deferral.Complete();
        }
        if (items.IconFiles.Count > 0 && targetUnderPointer is { } found)
        {
            await found.Target.DropAsync(found.Element, items.IconFiles);
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

    /// <summary>
    /// The part under the pointer that takes a drop there, and the element under the pointer it was found from: tried from the event's source, then from each element found at the pointer,
    /// since the source can be the element handling the drag rather than the innermost one.
    /// </summary>
    private static (IIconFileDropTarget Target, DependencyObject Element)? FindTargetUnderPointer(UIElement root, DragEventArgs eventArgs)
    {
        IEnumerable<DependencyObject> elementsUnderPointer = VisualTreeHelper.FindElementsInHostCoordinates(eventArgs.GetPosition(null), root);
        if (eventArgs.OriginalSource is DependencyObject source)
        {
            elementsUnderPointer = elementsUnderPointer.Prepend(source);
        }
        foreach (var element in elementsUnderPointer)
        {
            if (FindTarget(element) is { } target)
            {
                return (target, element);
            }
        }
        return null;
    }

    /// <summary>The nearest part, at or around <paramref name="element"/>, that takes a drop there.</summary>
    private static IIconFileDropTarget? FindTarget(DependencyObject element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is IIconFileDropTarget target && target.TakesDropOn(element))
            {
                return target;
            }
        }
        return null;
    }

    /// <summary>The drag's files once read; until then null, and their reading is started (once per drag).</summary>
    private static IconFileSelection? GetItemsOrStartReading(DataPackageView dataView)
    {
        if (!ReferenceEquals(dataView, _readDataView))
        {
            _readDataView = dataView;
            _readItems = null;
            _ = KeepItemsAsync(dataView);
        }
        return _readItems;
    }

    private static async Task KeepItemsAsync(DataPackageView dataView)
    {
        var items = await ReadAsync(dataView);
        // A newer drag may have started meanwhile.
        if (ReferenceEquals(dataView, _readDataView))
        {
            _readItems = items;
        }
    }

    /// <summary>The files a drag or the clipboard holds; empty when the app they came from doesn't hand them over.</summary>
    public static async Task<IconFileSelection> ReadAsync(DataPackageView dataView)
    {
        try
        {
            return IconFileSelection.FromPaths((await dataView.GetStorageItemsAsync()).Select(item => item.Path));
        }
        catch (COMException)
        {
            return IconFileSelection.None;
        }
    }
}
