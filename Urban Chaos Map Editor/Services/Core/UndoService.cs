// Services/Core/UndoService.cs
// Byte-level undo stack. Stores up to MaxEntries full map-byte snapshots.
// MapDataService calls RecordSnapshot() before every mutation; MainWindow
// calls TryUndo() on Ctrl+Z.

namespace UrbanChaosMapEditor.Services.Core
{
    public sealed class UndoService
    {
        private static readonly Lazy<UndoService> _lazy = new(() => new UndoService());
        public static UndoService Instance => _lazy.Value;

        private const int MaxEntries = 10;

        // List used as a bounded stack: index 0 = oldest, index Count-1 = newest.
        private readonly List<byte[]> _stack = new();

        // Prevents re-recording when ReplaceBytes is called internally during a restore.
        private bool _isRestoring;

        public bool CanUndo => _stack.Count > 0;
        public int Count => _stack.Count;

        private UndoService() { }

        /// <summary>
        /// Called by MapDataService before every mutation.
        /// <paramref name="snapshot"/> must already be a defensive copy of the current bytes
        /// (MapDataService passes GetBytesCopy() so no second clone is needed here).
        /// </summary>
        internal void RecordSnapshot(byte[] snapshot)
        {
            if (_isRestoring) return;

            _stack.Add(snapshot);

            // Drop the oldest entry once the cap is exceeded.
            if (_stack.Count > MaxEntries)
                _stack.RemoveAt(0);
        }

        /// <summary>
        /// Restores the most recent snapshot and removes it from the stack.
        /// Returns false when the stack is empty.
        /// <paramref name="remaining"/> is the number of further undo steps available.
        /// </summary>
        public bool TryUndo(out int remaining)
        {
            remaining = 0;
            if (_stack.Count == 0)
                return false;

            var snapshot = _stack[^1];
            _stack.RemoveAt(_stack.Count - 1);
            remaining = _stack.Count;

            _isRestoring = true;
            try
            {
                MapDataService.Instance.ReplaceBytes(snapshot);
            }
            finally
            {
                _isRestoring = false;
            }

            return true;
        }

        /// <summary>Discards all undo history. Call when a new map is loaded or the map is cleared.</summary>
        public void Clear() => _stack.Clear();
    }
}
