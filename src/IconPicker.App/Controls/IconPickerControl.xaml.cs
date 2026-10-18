using System.ComponentModel;
using IconPicker.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IconPicker.App.Controls;

/// <summary>The ranked icon grid with Browse, used by single mode and by batch mode's side panel.</summary>
public sealed partial class IconPickerControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel), typeof(IconPickerViewModel), typeof(IconPickerControl), new PropertyMetadata(null, OnViewModelChanged));

    private bool _isSyncingSelection;

    public IconPickerControl()
    {
        InitializeComponent();
    }

    public IconPickerViewModel? ViewModel
    {
        get => (IconPickerViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject owner, DependencyPropertyChangedEventArgs eventArgs)
    {
        var control = (IconPickerControl)owner;
        if (eventArgs.OldValue is IconPickerViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= control.OnViewModelPropertyChanged;
            oldViewModel.CandidatesRefreshed -= control.OnCandidatesRefreshed;
        }
        if (eventArgs.NewValue is IconPickerViewModel newViewModel)
        {
            newViewModel.PropertyChanged += control.OnViewModelPropertyChanged;
            newViewModel.CandidatesRefreshed += control.OnCandidatesRefreshed;
        }
        control.Bindings.Update();
        control.SyncSelection();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(IconPickerViewModel.Choice))
        {
            SyncSelection();
        }
    }

    private void OnCandidatesRefreshed(object? sender, EventArgs eventArgs)
    {
        SyncSelection();
    }

    /// <summary>The grid shows the chosen icon as selected whenever it is in view.</summary>
    private void SyncSelection()
    {
        _isSyncingSelection = true;
        var choice = ViewModel?.Choice;
        CandidateGrid.SelectedItem = choice is not null && ViewModel!.VisibleCandidates.Contains(choice) ? choice : null;
        _isSyncingSelection = false;
    }

    private void OnCandidateSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection || ViewModel is null)
        {
            return;
        }
        if (CandidateGrid.SelectedItem is CandidateViewModel candidate)
        {
            ViewModel.Choose(candidate);
        }
        else
        {
            // A click can't leave the folder without a pick.
            SyncSelection();
        }
    }
}
