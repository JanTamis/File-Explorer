using Avalonia.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FileExplorerCore.Extensions
{
	public static class NotifierExtensions
	{
		public static async ValueTask OnPropertyChanged(this INotifyPropertyChanged propertyChanged, [CallerMemberName] string? name = null)
		{
			if (Dispatcher.UIThread.CheckAccess())
			{
				propertyChanged.PropertyChanged?.Invoke(propertyChanged, new PropertyChangedEventArgs(name));
			}
			else
			{
				await Dispatcher.UIThread.InvokeAsync(() => propertyChanged.PropertyChanged?.Invoke(propertyChanged, new PropertyChangedEventArgs(name)));
			}
		}

		public static ValueTask OnPropertyChanged<T>(this INotifyPropertyChanged propertyChanged, ref T field, T value, [CallerMemberName] string? name = null)
		{
			field = value;

			return OnPropertyChanged(propertyChanged, name);
		}
	}
}