namespace FileExplorer.Core.Models;

public class MenuItemModel
{
	public MenuItemModel(MenuItemType type, string icon = "")
	{
		Type = type;
		Icon = icon ?? throw new ArgumentNullException(nameof(icon));
	}

	public MenuItemType Type { get; }
	public string Icon { get; }
}