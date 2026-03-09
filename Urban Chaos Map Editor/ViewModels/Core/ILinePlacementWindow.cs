namespace UrbanChaosMapEditor.ViewModels.Core
{
    /// <summary>
    /// Implemented by line-placement dialogs (Door/Gate/etc.) so MapViewModel can
    /// notify them when placement is cancelled or completed.
    /// </summary>
    public interface ILinePlacementWindow
    {
        void OnPlacementCancelled();
        void OnPlacementCompleted(int facetsAdded);
    }
}
