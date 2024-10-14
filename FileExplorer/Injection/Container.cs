using FileExplorer.Popup;
using FileExplorer.Services;
using FileExplorer.ViewModels;
using StrongInject;

namespace FileExplorer.Injection;

[Register<Properties>]
[Register<Settings>(Scope.SingleInstance)]
[Register<TabItemViewModel>]
[Register<FileOperationService>]
public partial class Container :
	IContainer<Properties>,
	IContainer<Settings>,
	IContainer<TabItemViewModel>,
	IContainer<FileOperationService>
{
}