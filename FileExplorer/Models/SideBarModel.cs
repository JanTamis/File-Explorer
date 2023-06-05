using Avalonia.Media;
using Humanizer;

namespace FileExplorer.Models;

public sealed class SideBarModel
{
	public Environment.SpecialFolder Folder { get; init; }

	public string Name { get; init; }
	
	public IImage? Icon { get; init; }

	public SideBarModel(Environment.SpecialFolder folder, string name, IImage? icon)
	{
		Folder = folder;
		Name = name;
		Icon = icon;
	}
}