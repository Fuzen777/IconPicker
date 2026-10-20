using System.ComponentModel;
using IconPicker.App.Services;
using IconPicker.App.ViewModels;
using IconPicker.Core.Detection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace IconPicker.App.Views;

/// <summary>
/// The open folder. The page is kept while other pages are visited, and shows whichever folder the navigator holds: the same one when coming back, as it was left.
/// </summary>
public sealed partial class FolderPage : Page, IIconFilePasteTarget
{
    private bool _isSyncingModeSelector;

    public FolderPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
        ToolTipService.SetToolTip(DeepToggle, Strings.Get("DeepToggleToolTip"));
        ToolTipService.SetToolTip(LibraryToggle, Strings.Get("LibraryToggleToolTip"));
    }

    public FolderViewModel? ViewModel { get; private set; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var viewModel = (FolderViewModel)e.Parameter;
        if (ReferenceEquals(viewModel, ViewModel))
        {
            // Back to the same folder: everything is as it was left.
            return;
        }
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
        ViewModel = viewModel;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Bindings.Update();
        ShowContent();
        await viewModel.InitializeAsync();
        // Single is the mode's default value, so detecting it raises no change to sync from.
        if (ReferenceEquals(viewModel, ViewModel))
        {
            SyncModeSelector();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        switch (eventArgs.PropertyName)
        {
            case nameof(FolderViewModel.Mode):
                SyncModeSelector();
                break;
            case nameof(FolderViewModel.Content):
                ShowContent();
                break;
        }
    }

    /// <summary>Pasted icon files go to the mode shown.</summary>
    Task<bool> IIconFilePasteTarget.PasteAsync(IReadOnlyList<string> iconFiles)
    {
        return ModeContent.Content is IIconFilePasteTarget mode ? mode.PasteAsync(iconFiles) : Task.FromResult(false);
    }

    private void ShowContent()
    {
        ModeContent.Content = ViewModel!.Content switch
        {
            SingleModeViewModel single => new SingleModeView(single),
            BatchModeViewModel batch => new BatchModeView(batch),
            _ => null,
        };
    }

    private void SyncModeSelector()
    {
        _isSyncingModeSelector = true;
        ModeSelector.SelectedItem = ViewModel!.Mode == FolderMode.Single ? SingleModeItem : BatchModeItem;
        _isSyncingModeSelector = false;
    }

    private async void OnModeSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_isSyncingModeSelector || ViewModel is null || sender.SelectedItem is null)
        {
            return;
        }
        await ViewModel.SwitchModeAsync(ReferenceEquals(sender.SelectedItem, SingleModeItem) ? FolderMode.Single : FolderMode.Batch);
        // Puts the selector back when switching was cancelled.
        SyncModeSelector();
    }
}
