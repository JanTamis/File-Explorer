namespace FileExplorer.Core.Models;

public sealed class MenuItemModel
{
	public MenuItemModel(MenuItemType type, string icon = "", Action<MenuItemActionModel>? action = null)
	{
		Type = type;
		Action = action;
		Icon = icon ?? throw new ArgumentNullException(nameof(icon));
	}

	public MenuItemType Type { get; }
	public string Icon { get; }
	public Action<MenuItemActionModel>? Action { get; set; }
}