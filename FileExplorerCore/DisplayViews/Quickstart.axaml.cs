using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FileExplorerCore.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using FileExplorerCore.ViewModels;

namespace FileExplorerCore.DisplayViews
{
	public partial class Quickstart : UserControl
	{
		private IEnumerable<FileModel> RecentFiles
		{
			get
			{
				FileModel.ImageSize = 128;

				return new FileSystemEnumerable<FileModel>(Environment.GetFolderPath(Environment.SpecialFolder.Recent), TabItemViewModel.GetFileModel).Take(20);
			}
		}

		private IEnumerable<FileModel> Drives
		{
			get
			{
				FileModel.ImageSize = 128;

				return DriveInfo
					.GetDrives()
					.Where(x => x.IsReady)
					.Select(x => new FileModel(x.RootDirectory.FullName, true));
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
