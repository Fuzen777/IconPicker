using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IconPicker.App.Services;
using IconPicker.Core.Applying;
using IconPicker.Core.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace IconPicker.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _settings;
    private readonly ContextMenuRegistration _contextMenu;
    private readonly DialogService _dialogs;
    private readonly WindowContext _windowContext;

    internal SettingsViewModel(IServiceProvider services)
    {
        _settings = services.GetRequiredService<SettingsStore>();
        _windowContext = services.GetRequiredService<WindowContext>();
        _contextMenu = services.GetRequiredService<ContextMenuRegistration>();
        _dialogs = services.GetRequiredService<DialogService>();
        Libraries = new LibrariesViewModel(services, isForRescanning: false);
        ContextMenuState = _contextMenu.GetState();
    }

    public LibrariesViewModel Libraries { get; }

    public string VersionText => Strings.Format("AboutVersion", Assembly.GetExecutingAssembly().GetName().Version?.ToString(3));

    /// <summary>The theme as the combo box's index: 0 for Windows' own, 1 for Light, 2 for Dark.</summary>
    public int ThemeIndex
    {
        get => (int)_settings.Current.Theme;
        set
        {
            if (value < 0 || value == ThemeIndex)
            {
                return;
            }
            _settings.Update(settings => settings.Theme = (AppTheme)value);
            OnPropertyChanged();
        }
    }

    /// <summary>The Icon target as the combo box's index: 0 for Copy, 1 for Link.</summary>
    public int IconTargetIndex
    {
        get => (int)_settings.Current.IconTarget;
        set
        {
            if (value < 0 || value == IconTargetIndex)
            {
                return;
            }
            _settings.Update(settings => settings.IconTarget = (IconTarget)value);
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContextMenuOn), nameof(IsContextMenuElsewhere), nameof(ContextMenuDescription))]
    public partial ContextMenuState ContextMenuState { get; private set; }

    public bool IsContextMenuOn
    {
        get => ContextMenuState != ContextMenuState.Off;
        set
        {
            if (value == IsContextMenuOn)
            {
                return;
            }
            if (value)
            {
                _contextMenu.Register();
            }
            else
            {
                _contextMenu.Unregister();
            }
            ContextMenuState = _contextMenu.GetState();
        }
    }

    public bool IsContextMenuElsewhere => ContextMenuState == ContextMenuState.Elsewhere;

    /// <summary>The app's folder can take a Data folder: not one only admins may write to, such as Program Files.</summary>
    public bool CanChangeDataLocation { get; } = DataLocation.CanBePortable();

    /// <summary>Data kept in a Data folder next to the app. Turning it on or off moves the data and restarts the app, after asking.</summary>
    public bool IsPortable
    {
        get => DataLocation.IsPortable;
        set
        {
            if (value != IsPortable)
            {
                _ = ConfirmDataMoveAsync(value);
            }
        }
    }

    public string DataLocationDescription
    {
        get
        {
            if (!CanChangeDataLocation)
            {
                return Strings.Format("DataLocationReadOnlyDescription", DataLocation.AppDataFolder);
            }
            return Strings.Format(IsPortable ? "DataLocationPortableDescription" : "DataLocationAppDataDescription", DataLocation.Folder);
        }
    }

    public string ContextMenuDescription => ContextMenuState == ContextMenuState.Elsewhere
        ? Strings.Format("ContextMenuElsewhereDescription", _contextMenu.GetRegisteredCommand())
        : Strings.Get("ContextMenuDescription");

    [RelayCommand]
    private void PointContextMenuHere()
    {
        _contextMenu.Register();
        ContextMenuState = _contextMenu.GetState();
    }

    private async Task ConfirmDataMoveAsync(bool toPortable)
    {
        var destination = toPortable ? DataLocation.PortableFolder : DataLocation.AppDataFolder;
        if (await _dialogs.ConfirmAsync(Strings.Get("DataMoveConfirmTitle"), Strings.Format("DataMoveConfirmMessage", destination), Strings.Get("DataMoveConfirmButton")))
        {
            try
            {
                // Saved now, so the new copy finds it in the moved data; it waits for this one to exit before moving anything.
                _windowContext.Window.SavePlacement();
                DataLocation.RestartToMove(toPortable);
                Application.Current.Exit();
                return;
            }
            catch (Win32Exception exception)
            {
                ErrorLog.Write(exception);
                await _dialogs.ShowMessageAsync(Strings.Get("DataMoveFailedTitle"), Strings.Format("DataMoveFailedMessage", exception.Message, DataLocation.Folder));
            }
        }
        // Puts the switch back.
        OnPropertyChanged(nameof(IsPortable));
    }

    [RelayCommand]
    private async Task RestartExplorerAsync()
    {
        if (await _dialogs.ConfirmAsync(Strings.Get("RestartExplorerConfirmTitle"), Strings.Get("RestartExplorerConfirmMessage"), Strings.Get("RestartExplorerConfirmButton")))
        {
            await ExplorerRestarter.RestartAsync();
        }
    }
}
