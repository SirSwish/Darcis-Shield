// /Views/Dialogs/Buildings/IFacetMultiDrawWindow.cs
namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    public interface IFacetMultiDrawWindow
    {
        /// <summary>
        /// Called when drawing is cancelled (right-click with no facets drawn).
        /// </summary>
        void OnDrawCancelled();

        /// <summary>
        /// Called when drawing is completed (right-click after drawing facets).
        /// </summary>
        void OnDrawCompleted(int facetsAdded);
    }
}