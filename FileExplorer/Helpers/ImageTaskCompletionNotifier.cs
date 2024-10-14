using Avalonia.Media;

namespace FileExplorer.Helpers;

public sealed class ImageTaskCompletionNotifier : TaskCompletionNotifier<IImage?>
{
	public ImageTaskCompletionNotifier(Task<IImage?> task, IImage? defaultValue = default) : base(task, defaultValue)
	{
	}
}