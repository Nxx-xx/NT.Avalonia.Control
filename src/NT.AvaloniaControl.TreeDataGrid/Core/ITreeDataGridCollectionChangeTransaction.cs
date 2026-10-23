namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>Internal handshake for controlled storage commits whose indexed collection notification is still pending.</summary>
internal interface ITreeDataGridCollectionChangeTransaction
{
    bool IsChangePending { get; }
    event EventHandler? ChangeCompleted;
}
