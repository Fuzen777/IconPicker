using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IconPicker.App.Services;

internal sealed class DialogService(WindowContext windowContext)
{
    // WinUI refuses to show a second dialog while one is open.
    private readonly SemaphoreSlim _oneDialogAtATime = new(1);

    /// <summary>Asks a yes/no question. Returns true when the user confirms.</summary>
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        var dialog = CreateDialog(title, message);
        dialog.PrimaryButtonText = confirmText;
        dialog.CloseButtonText = Strings.Get("CancelButton");
        dialog.DefaultButton = ContentDialogButton.Primary;
        return await ShowAsync(dialog) == ContentDialogResult.Primary;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = CreateDialog(title, message);
        dialog.CloseButtonText = Strings.Get("OkButton");
        dialog.DefaultButton = ContentDialogButton.Close;
        await ShowAsync(dialog);
    }

    /// <summary>Asks for one line of text, offering <paramref name="text"/> selected. Null when cancelled.</summary>
    public async Task<string?> PromptTextAsync(string title, string label, string text, string confirmText)
    {
        var textBox = new TextBox { Header = label, Text = text };
        textBox.Loaded += (_, _) =>
        {
            textBox.Focus(FocusState.Programmatic);
            textBox.SelectAll();
        };
        var dialog = new ContentDialog
        {
            Title = title,
            Content = textBox,
            PrimaryButtonText = confirmText,
            CloseButtonText = Strings.Get("CancelButton"),
            DefaultButton = ContentDialogButton.Primary,
        };
        return await ShowAsync(dialog) == ContentDialogResult.Primary ? textBox.Text : null;
    }

    /// <summary>Shows any dialog in its turn, attached to the main window and in the default dialog style unless it has its own.</summary>
    public async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        dialog.XamlRoot ??= windowContext.XamlRoot;
        // A dialog sits outside the window's content, so it takes the theme chosen in Settings from there.
        if (dialog.XamlRoot.Content is FrameworkElement windowContent)
        {
            dialog.RequestedTheme = windowContent.RequestedTheme;
        }
        dialog.Style ??= (Style)Application.Current.Resources["DefaultContentDialogStyle"];
        await _oneDialogAtATime.WaitAsync();
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            _oneDialogAtATime.Release();
        }
    }

    private static ContentDialog CreateDialog(string title, string message)
    {
        return new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
        };
    }
}
