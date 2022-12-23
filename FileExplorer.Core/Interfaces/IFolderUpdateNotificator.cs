using FileExplorer.Core.Models;

namespace FileExplorer.Core.Interfaces;

public interface IFolderUpdateNotificator : IDisposable, IEquatable<IFolderUpdateNotificator>
{
	event Action<IFolderUpdateNotificator, ChangeType, string, string?> Changed;
}