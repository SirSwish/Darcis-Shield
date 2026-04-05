using System.Windows;

namespace UrbanChaosMapEditor.Services.Roofs
{
    /// <summary>
    /// Manages "Apply to Selection" mode for RF4 tile painting.
    /// The RF4 editor calls BeginSelection with a callback; the RoofsLayer
    /// handles the drag and calls CompleteSelection or Cancel.
    /// SelectionEnded fires for both completion and cancellation.
    /// </summary>
    public sealed class RoofTileSelectionService
    {
        public static RoofTileSelectionService Instance { get; } = new();
        private RoofTileSelectionService() { }

        private Action<Rect>? _callback;

        public bool IsSelecting { get; private set; }

        /// <summary>Fired when selection mode starts.</summary>
        public event EventHandler? SelectionBegan;

        /// <summary>Fired when selection mode ends (completion or cancellation).</summary>
        public event EventHandler? SelectionEnded;

        public void BeginSelection(Action<Rect> onComplete)
        {
            _callback = onComplete;
            IsSelecting = true;
            SelectionBegan?.Invoke(this, EventArgs.Empty);
        }

        public void CompleteSelection(Rect rect)
        {
            if (!IsSelecting) return;
            IsSelecting = false;
            var cb = _callback;
            _callback = null;
            SelectionEnded?.Invoke(this, EventArgs.Empty);
            cb?.Invoke(rect);
        }

        public void Cancel()
        {
            if (!IsSelecting) return;
            IsSelecting = false;
            _callback = null;
            SelectionEnded?.Invoke(this, EventArgs.Empty);
        }
    }
}
