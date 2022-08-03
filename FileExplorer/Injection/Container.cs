using FileExplorer.Popup;
using StrongInject;

namespace FileExplorer.Injection;

[Register<Properties>]
[Register<Settings>]
public partial class Container :
	IContainer<Properties>,
	IContainer<Settings>
{
}