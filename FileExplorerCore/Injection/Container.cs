using FileExplorerCore.Popup;
using StrongInject;

namespace FileExplorerCore.Injection;

[Register<Properties>]
[Register<Settings>]
public partial class Container : 
	IAsyncContainer<Properties>, IContainer<Properties>,
	IAsyncContainer<Settings>, IContainer<Settings>
{
}