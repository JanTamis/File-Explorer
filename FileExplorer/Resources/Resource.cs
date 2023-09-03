using Avalonia.Markup.Xaml;

namespace FileExplorer.Resources;

public class Resource : MarkupExtension
{
	public string? Name { get; init; }

	public Resource(string? name)
	{
		Name = name;
	}

	public override object ProvideValue(IServiceProvider serviceProvider)
	{
		return ResourceDefault.ResourceManager.GetString(Name) ?? String.Empty;
	}
}