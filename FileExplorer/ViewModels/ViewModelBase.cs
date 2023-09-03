using Avalonia.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileExplorer.ViewModels;

public sealed class ViewModelBase : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged = delegate { };

	public async ValueTask OnPropertyChanged([CallerMemberName] string? name = null)
	{
		if (Dispatcher.UIThread.CheckAccess())
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
		else
		{
			await Dispatcher.UIThread.InvokeAsync(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)));
		}
	}

	public ValueTask OnPropertyChanged<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		field = value;
		return OnPropertyChanged(name);
	}
}