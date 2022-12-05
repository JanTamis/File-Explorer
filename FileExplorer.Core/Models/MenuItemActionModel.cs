using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;

namespace FileExplorer.Core.Models;

public class MenuItemActionModel
{
	public ObservableRangeCollection<IFileItem> Files { get; init; }
	public IFileItem CurrentFolder { get; init; }
	public IPopup Popup { get; set; }
}