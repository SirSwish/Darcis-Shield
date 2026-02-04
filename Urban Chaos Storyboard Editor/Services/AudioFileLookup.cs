using System.Collections.Generic;

namespace UrbanChaosStoryboardEditor.Services
{
    /// <summary>
    /// Maps mission IDs to their briefing audio filenames.
    /// </summary>
    public static class AudioFileLookup
    {
        private static readonly Dictionary<int, string> IdToFileMap = new()
        {
            { 6, "policem1.wav" },
            { 9, "policem2.wav" },
            { 12, "policem3.wav" },
            { 14, "policem4.wav" },
            { 15, "policem5.wav" },
            { 16, "policem6.wav" },
            { 17, "policem7.wav" },
            { 18, "policem8.wav" },
            { 19, "policem9.wav" },
            { 20, "policem10.wav" },
            { 21, "policem11.wav" },
            { 22, "policem12.wav" },
            { 23, "policem13.wav" },
            { 24, "policem14.wav" },
            { 25, "policem15.wav" },
            { 26, "policem16.wav" },
            { 27, "policem17.wav" },
            { 28, "policem18.wav" },
            { 29, "roperm19.wav" },
            { 30, "roperm20.wav" },
            { 31, "roperm21.wav" },
            { 32, "roperm23.wav" },
            { 33, "roperm24.wav" }
        };

        public static string GetFileNameById(int id)
        {
            return IdToFileMap.TryGetValue(id, out string? fileName) ? fileName : "POLICEM.wav";
        }
    }
}