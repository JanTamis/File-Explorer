using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FileExplorerCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FileExplorerCore.DisplayViews;

public partial class Quickstart : UserControl
{
	private IEnumerable<FileModel> RecentFiles
	{
		get
		{
			var path = Environment.GetFolderPath(Environment.SpecialFolder.Recent);

			//return !String.IsNullOrEmpty(path) 
			//	? new FileSystemEnumerable<FileModel>(Environment.GetFolderPath(Environment.SpecialFolder.Recent), TabItemViewModel.GetFileModel).Take(20) 
			//	: Enumerable.Empty<FileModel>();

			return Enumerable.Empty<FileModel>();
		}
	}

	private IEnumerable<FileModel> Drives
	{
		get
		{
			//return MainWindowViewModel.Tree.Children
			//	.Select(s => new FileModel(s));

			return Enumerable.Empty<FileModel>();

			//return DriveInfo
			//	.GetDrives()
			//	.Where(x => x.IsReady)
			//	.Select(x => new FileModel(x.Name, true));
		}
	}

	private int DriveCount => Drives.Count();
	private int FileCount => RecentFiles.Count();

	public Quickstart()
	{
		InitializeComponent();

		DataContext = this;
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}