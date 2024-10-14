using Avalonia;
using Avalonia.Controls;
using FileExplorer.Core.Models;
using FileExplorer.Interfaces;

namespace FileExplorer.Providers.FileSystemMenuItems;

public class SeparatorMenuItemModel : IMenuModel
{
	public Control GetControl(MenuItemActionModel actionModel)
	{
		return new Border
		{
			Width = 4,
			Margin = new Thickness(4, 0),
			Classes = { "Separator" }
		};
	}
}