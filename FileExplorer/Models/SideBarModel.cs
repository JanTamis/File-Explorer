using Avalonia.Media;

namespace FileExplorer.Models;

public sealed class SideBarModel(string? folder, string name, IImage? icon)
{
	public string? Folder { get; } = folder;

	public string Name { get; init; } = name;

	public IImage? Icon { get; init; } = icon;

	public SideBarModel(Environment.SpecialFolder folder, string name, IImage? icon)
		: this(Environment.GetFolderPath(folder), name, icon)
	{
		
	}
}