using System.ComponentModel;
using IconPicker.App.Services;
using IconPicker.App.ViewModels;
using IconPicker.Core.Detection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace IconPicker.App.Views;

public sealed partial class FolderPage : Page, IHasUnappliedChanges
{
    private bool _isSyncingModeSelector;

    public FolderPage()
    {
        InitializeComponent();
        ToolTipService.SetToolTip(DeepToggle, Strings.Get("DeepToggleToolTip"));
        ToolTipService.SetToolTip(LibraryToggle, Strings.Get("LibraryToggleToolTip"));
    }

    public FolderViewModel? ViewModel { get; private set; }

    public bool HasUnappliedChanges => ViewModel?.HasUnappliedChanges == true;

    public string DiscardChangesTitle => Strings.Get("DiscardChangesTitle");

    public string DiscardChangesMessage => Strings.Get("DiscardChangesMessage");

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel = new FolderViewModel((FolderRequest)e.Parameter, App.Services);
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Bindings.Update();
        await ViewModel.InitializeAsync();
        // Single is the mode's default value, so detecting it raises no change to sync from.
        SyncModeSelector();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel?.Dispose();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        switch (eventArgs.PropertyName)
        {
            case nameof(FolderViewModel.Mode):
                SyncModeSelector();
                break;
            case nameof(FolderViewModel.Content):
                ModeContent.Content = ViewModel!.Content switch
                {
                    SingleModeViewModel single => new SingleModeView(single),
                    BatchModeViewModel batch => new BatchModeView(batch),
                    _ => null,
                };
                break;
        }
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
