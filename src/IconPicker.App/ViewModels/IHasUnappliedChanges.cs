namespace IconPicker.App.ViewModels;

/// <summary>A page holding work that leaving it would lose, so the navigator asks first.</summary>
internal interface IHasUnappliedChanges
{
    bool HasUnappliedChanges { get; }

    /// <summary>The question asked before leaving.</summary>
    string DiscardChangesTitle { get; }

    string DiscardChangesMessage { get; }
}
