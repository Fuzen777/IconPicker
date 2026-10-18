using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;

namespace IconPicker.App.ViewModels;

/// <summary>The message bar at the top of a mode: results of applying, errors, and an optional action.</summary>
public sealed partial class StatusMessage : ObservableObject
{
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial InfoBarSeverity Severity { get; private set; }

    [ObservableProperty]
    public partial string? Title { get; private set; }

    [ObservableProperty]
    public partial string? Message { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAction))]
    public partial string? ActionText { get; private set; }

    [ObservableProperty]
    public partial ICommand? ActionCommand { get; private set; }

    public bool HasAction => ActionText is not null;

    public void Show(InfoBarSeverity severity, string title, string? message = null, string? actionText = null, ICommand? actionCommand = null)
    {
        Severity = severity;
        Title = title;
        Message = message;
        ActionText = actionText;
        ActionCommand = actionCommand;
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
    }
}
