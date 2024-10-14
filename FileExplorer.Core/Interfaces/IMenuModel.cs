using Avalonia.Controls;
using FileExplorer.Core.Models;

namespace FileExplorer.Interfaces;

public interface IMenuModel
{
	Control GetControl(MenuItemActionModel actionModel);
}