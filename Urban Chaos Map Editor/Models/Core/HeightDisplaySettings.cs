namespace UrbanChaosMapEditor.Models.Core
{
    /// <summary>
    /// Global toggle: whether height values are shown as raw file bytes or Quarter Storeys.
    /// Quarter Storeys = raw Y / 2.  Raw Y = QS * 2.
    /// </summary>
    public static class HeightDisplaySettings
    {
        private static bool _showRawHeights;

        public static bool ShowRawHeights
        {
            get => _showRawHeights;
            set
            {
                if (_showRawHeights == value) return;
                _showRawHeights = value;
                DisplayModeChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>Fired on the calling thread whenever <see cref="ShowRawHeights"/> changes.</summary>
        public static event EventHandler? DisplayModeChanged;
    }
}
