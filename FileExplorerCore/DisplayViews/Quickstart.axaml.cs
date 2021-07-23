using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FileExplorerCore.Models;
using NetFabric.Hyperlinq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;

namespace FileExplorerCore.DisplayViews
{
	public partial class Quickstart : UserControl
	{
		private IEnumerable<FileModel> RecentFiles
		{
			get
			{
				return new FileSystemEnumerable<FileModel>(Environment.GetFolderPath(Environment.SpecialFolder.Recent), (ref FileSystemEntry x) => new FileModel(x.ToFullPath(), x.IsDirectory, 128)).Take(20);
			}
		}

		private IEnumerable<FileModel> Drives
		{
			get
			{
				return DriveInfo.GetDrives()
												.AsValueEnumerable()
												.Where(x => x.IsReady)
												.Select(x => new FileModel(x.RootDirectory.FullName, true, 128));
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
}
