using Avalonia.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FileExplorerCore.ViewModels;

public class ViewModelBase : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler PropertyChanged = delegate { };

	public async ValueTask OnPropertyChanged([CallerMemberName] string? name = null)
	{
		if (Dispatcher.UIThread.CheckAccess())
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
		else
		{
			await Dispatcher.UIThread.InvokeAsync(() => PropertyChanged(this, new PropertyChangedEventArgs(name)));
		}
	}

	public ValueTask OnPropertyChanged<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		field = value;
		return OnPropertyChanged(name);
	}
}