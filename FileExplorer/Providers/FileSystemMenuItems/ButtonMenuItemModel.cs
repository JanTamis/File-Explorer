using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml.MarkupExtensions;
using CommunityToolkit.Mvvm.Input;
using FileExplorer.Core.Models;
using FileExplorer.Interfaces;
using Material.Icons;
using Material.Icons.Avalonia;

namespace FileExplorer.Providers.FileSystemMenuItems;

public class ButtonMenuItemModel(MaterialIconKind icon, Action<MenuItemActionModel> action) : IMenuModel
{
	public Control GetControl(MenuItemActionModel actionModel)
	{
		var button = new Button
		{
			[!TemplatedControl.ForegroundProperty] = new DynamicResourceExtension("MaterialBodyBrush"),
			Classes = { "Icon", },
			Content = new MaterialIcon
			{
				Width = 25,
				Height = 25,
				[!TemplatedControl.ForegroundProperty] = new DynamicResourceExtension("MaterialPrimaryMidForegroundBrush"),
				Kind = icon,
			},
			Command = new RelayCommand(() =>
			{
				action(actionModel);
			}),
		};

		return button;
	}
}