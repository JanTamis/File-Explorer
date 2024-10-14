using Avalonia.Markup.Xaml;

namespace FileExplorer.Resources;

public class Resource(string name) : MarkupExtension
{
	public override object ProvideValue(IServiceProvider serviceProvider)
	{
		return ResourceDefault.ResourceManager.GetString(name) ?? String.Empty;
	}
}