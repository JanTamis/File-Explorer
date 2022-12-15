using FileExplorer.Core.Models;

namespace FileExplorer.Core.Interfaces;

public interface IFolderUpdateNotificator : IDisposable
{
	event Action<ChangeType, string, string?> Changed;
}