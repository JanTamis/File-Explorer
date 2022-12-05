using FileExplorer.Popup;
using StrongInject;

namespace FileExplorer.Injection;

[Register<Properties>]
[Register<Settings>]
public sealed partial class Container :
	IContainer<Properties>,
	IContainer<Settings>
{
}