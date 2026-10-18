using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IconPicker.App.Services;
using IconPicker.Core.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;

namespace IconPicker.App.ViewModels;

/// <summary>The My icons tab: the saved icons, most recently used first, and the builder beside them.</summary>
public sealed partial class MyIconsViewModel : ObservableObject, IHasUnappliedChanges, IDisposable
{
    private readonly MyIconsStore _store;
    private readonly DialogService _dialogs;
    private readonly ThumbnailLoader _thumbnailLoader;
    private readonly DispatcherQueue _dispatcher;

    internal MyIconsViewModel(IServiceProvider services)
    {
        _store = services.GetRequiredService<MyIconsStore>();
        _dialogs = services.GetRequiredService<DialogService>();
        _thumbnailLoader = services.GetRequiredService<ThumbnailLoader>();
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        Builder = new IconBuilderViewModel(services, isForFolder: false);
        Builder.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(IconBuilderViewModel.IsDirty))
            {
                OnPropertyChanged(nameof(HasUnappliedChanges));
            }
        };
        _store.Changed += OnStoreChanged;
        RefreshSavedIcons();
    }

    public IconBuilderViewModel Builder { get; }

    public ObservableCollection<SavedIconViewModel> SavedIcons { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(DuplicateCommand), nameof(RenameCommand), nameof(DeleteCommand))]
    public partial SavedIconViewModel? SelectedIcon { get; set; }

    public bool HasSelection => SelectedIcon is not null;

    public bool IsEmpty => SavedIcons.Count == 0;

    public bool HasUnappliedChanges => Builder.IsDirty;

    public string DiscardChangesTitle => Strings.Get("DiscardIconChangesTitle");

    public string DiscardChangesMessage => Strings.Get("DiscardIconChangesMessage");

    public void Dispose()
    {
        _store.Changed -= OnStoreChanged;
    }

    [RelayCommand]
    private async Task NewIconAsync()
    {
        if (await ConfirmDiscardingAsync())
        {
            Builder.Reset();
            SelectedIcon = null;
        }
    }

    /// <summary>The empty tab's invitation: a new icon, starting from the built-in icons.</summary>
    [RelayCommand]
    private async Task StartFromBuiltInIconsAsync()
    {
        if (await ConfirmDiscardingAsync())
        {
            Builder.Reset();
            Builder.ShowBuiltInIconsCommand.Execute(null);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedIcon is { } selectedIcon && await ConfirmDiscardingAsync())
        {
            Builder.LoadSavedIcon(selectedIcon.SavedIcon);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DuplicateAsync()
    {
        if (SelectedIcon is { } selectedIcon)
        {
            var copy = await _store.DuplicateAsync(selectedIcon.SavedIcon, Strings.Format("DuplicateIconName", selectedIcon.Name));
            RefreshAndSelect(copy.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RenameAsync()
    {
        if (SelectedIcon is not { } selectedIcon)
        {
            return;
        }
        var name = await _dialogs.PromptTextAsync(Strings.Get("RenameIconTitle"), Strings.Get("RenameIconLabel"), selectedIcon.Name, Strings.Get("RenameIconConfirm"));
        if (string.IsNullOrWhiteSpace(name) || name.Trim() == selectedIcon.Name)
        {
            return;
        }
        var renamed = await _store.RenameAsync(selectedIcon.SavedIcon, name.Trim());
        Builder.FollowRename(renamed);
        RefreshAndSelect(renamed.Id);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedIcon is not { } selectedIcon
            || !await _dialogs.ConfirmAsync(Strings.Format("DeleteIconTitle", selectedIcon.Name), Strings.Get("DeleteIconMessage"), Strings.Get("DeleteIconConfirm")))
        {
            return;
        }
        _store.Delete(selectedIcon.SavedIcon);
        if (Builder.EditedIcon?.Id == selectedIcon.SavedIcon.Id)
        {
            Builder.Reset();
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await Builder.SaveCommand.ExecuteAsync(null);
        if (Builder.EditedIcon is { } savedIcon)
        {
            RefreshAndSelect(savedIcon.Id);
        }
    }

    private async Task<bool> ConfirmDiscardingAsync()
    {
        return !Builder.IsDirty || await _dialogs.ConfirmAsync(DiscardChangesTitle, DiscardChangesMessage, Strings.Get("DiscardChangesConfirm"));
    }

    private void OnStoreChanged(object? sender, EventArgs eventArgs)
    {
        // The store may report from a background thread.
        _dispatcher.TryEnqueue(RefreshSavedIcons);
    }

    private void RefreshSavedIcons()
    {
        var selectedId = SelectedIcon?.SavedIcon.Id;
        SavedIcons.Clear();
        foreach (var savedIcon in _store.List())
        {
            SavedIcons.Add(new SavedIconViewModel(savedIcon, _thumbnailLoader));
        }
        OnPropertyChanged(nameof(IsEmpty));
        Select(selectedId);
    }

    /// <summary>Refreshes at once rather than when the store's report arrives, so the icon just made can be selected.</summary>
    private void RefreshAndSelect(string id)
    {
        RefreshSavedIcons();
        Select(id);
    }

    private void Select(string? id)
    {
        SelectedIcon = id is null ? null : SavedIcons.FirstOrDefault(savedIcon => savedIcon.SavedIcon.Id == id);
    }
}
