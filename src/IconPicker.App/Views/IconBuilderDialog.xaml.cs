using System.ComponentModel;
using IconPicker.App.Services;
using IconPicker.App.ViewModels;
using IconPicker.Core.Builder;
using IconPicker.Core.Candidates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace IconPicker.App.Views;

/// <summary>
/// The builder opened from a folder's picker. It starts on the icon list (Icons), where a saved icon is picked as it is and a built-in one moves on to the builder, or straight on the builder (an SVG from Browse, Customize). Only one dialog shows at a time, so everything happens inside this one.
/// </summary>
public sealed partial class IconBuilderDialog : ContentDialog, IIconFileDropTarget
{
    internal IconBuilderDialog(IServiceProvider services)
    {
        List = new IconListViewModel(services, showsSavedIcons: true);
        Builder = new IconBuilderViewModel(services, isForFolder: true);
        InitializeComponent();
        Opened += (_, _) => FitToWindow();
        List.SavedIconPicked += OnSavedIconPicked;
        List.BuiltInIconPicked += OnBuiltInIconPicked;
        Builder.PropertyChanged += OnBuilderPropertyChanged;
    }

    public IconListViewModel List { get; }

    public IconBuilderViewModel Builder { get; }

    public bool IsListShown { get; private set; }

    public bool IsBuilderShown => !IsListShown;

    /// <summary>The icon built for the folder, or null when cancelled or a saved icon was picked.</summary>
    internal IconCandidate? Result { get; private set; }

    /// <summary>The saved icon picked as it is from the list, or null.</summary>
    internal SavedIcon? PickedSavedIcon { get; private set; }

    public void ShowList()
    {
        IsListShown = true;
        Title = Strings.Get("IconListDialogTitle");
        PrimaryButtonText = "";
        Bindings.Update();
    }

    public void ShowBuilder()
    {
        IsListShown = false;
        Title = Strings.Get("IconBuilderDialogTitle");
        PrimaryButtonText = Strings.Get("UseForFolderButton");
        IsPrimaryButtonEnabled = Builder.HasSource;
        Bindings.Update();
    }

    /// <summary>The list takes dropped files by opening the builder on them; the builder takes them itself.</summary>
    bool IIconFileDropTarget.TakesDropOn(DependencyObject element)
    {
        return IsListShown;
    }

    async Task IIconFileDropTarget.DropAsync(DependencyObject element, IReadOnlyList<string> iconFiles)
    {
        await Builder.OpenFileAsync(iconFiles[0]);
        ShowBuilder();
    }

    /// <summary>The dialog sits above the window, whose own drop handling can't reach it; a folder can't be opened from here.</summary>
    private void OnDragOver(object sender, DragEventArgs eventArgs)
    {
        IconFileDrop.ShowDrag(ContentGrid, eventArgs, opensFolders: false);
    }

    private async void OnDrop(object sender, DragEventArgs eventArgs)
    {
        await IconFileDrop.DropAsync(ContentGrid, eventArgs);
    }

    /// <summary>Ctrl+V outside a text box: the first copied icon file becomes the builder's glyph, opening the builder from the list.</summary>
    private async void OnContentKeyDown(object sender, KeyRoutedEventArgs eventArgs)
    {
        if (!IconFilePaste.IsPasteKey(eventArgs))
        {
            return;
        }
        eventArgs.Handled = true;
        var pasted = await IconFilePaste.ReadClipboardAsync();
        if (pasted.IconFiles.Count > 0)
        {
            await Builder.OpenFileAsync(pasted.IconFiles[0]);
            ShowBuilder();
        }
    }

    /// <summary>As large as the builder needs, but never wider or taller than the window allows (it can be as narrow as 760 px).</summary>
    private void FitToWindow()
    {
        var windowSize = XamlRoot.Size;
        ContentGrid.Width = Math.Max(320, Math.Min(940, windowSize.Width - 120));
        ContentGrid.Height = Math.Max(240, Math.Min(580, windowSize.Height - 260));
    }

    private void OnSavedIconPicked(object? sender, SavedIcon savedIcon)
    {
        PickedSavedIcon = savedIcon;
        Hide();
    }

    private void OnBuiltInIconPicked(object? sender, GlyphSource source)
    {
        Builder.LoadSource(source);
        ShowBuilder();
    }

    private void OnBuilderPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(IconBuilderViewModel.Source) && IsBuilderShown)
        {
            IsPrimaryButtonEnabled = Builder.HasSource;
        }
    }

    /// <summary>Builds the icon before closing; when it can't be drawn, the dialog stays open on the builder's message.</summary>
    /// <remarks>The dialog's content stays live while it builds; the builder takes the look as it was when the button was pressed.</remarks>
    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            Result = await Builder.CreateCandidateAsync();
            args.Cancel = Result is null;
        }
        finally
        {
            deferral.Complete();
        }
    }
}
