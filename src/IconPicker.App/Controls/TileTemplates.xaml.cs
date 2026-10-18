using Microsoft.UI.Xaml;

namespace IconPicker.App.Controls;

/// <summary>Tile templates shared by several grids; a class of its own because its templates use x:Bind.</summary>
public sealed partial class TileTemplates : ResourceDictionary
{
    public TileTemplates()
    {
        InitializeComponent();
    }
}
