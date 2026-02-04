// /Views/Dialogs/Buildings/IFacetMultiDrawWindow.cs
using System;

namespace UrbanChaosMapEditor.Views.Dialogs.Buildings
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