using FileExplorer.Popup;
using StrongInject;

namespace FileExplorer.Injection;

[Register<Properties>]
[Register<Settings>]
public partial class Container : 
	IAsyncContainer<Properties>, IContainer<Properties>,
	IAsyncContainer<Settings>, IContainer<Settings>
{
}