// /Services/IUiDialogService.cs
namespace UrbanChaosMapEditor.Services.Core
{
    public interface IUiDialogService
    {
        string? OpenFile(string title, string filter);
        string? SaveFile(string title, string filter, string? suggestedName = null);
        bool Confirm(string message, string caption);
        void Info(string message, string caption);
    }
}
