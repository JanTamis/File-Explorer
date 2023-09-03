using System.IO;
using Avalonia.Controls;
using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;
using FileExplorer.Interfaces;
using FileExplorer.Models;

namespace FileExplorer.DisplayViews;

public sealed partial class Quickstart : UserControl, IFileViewer
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

	private IEnumerable<DriveModel> Drives =>
		DriveInfo
			.GetDrives()
			.Where(w => w is { IsReady: true, })
			.Select(s =>
			{
				if (OperatingSystem.IsWindows())
				{
					return new DriveModel(s.RootDirectory.FullName, s.VolumeLabel, s.TotalSize, s.TotalSize - s.AvailableFreeSpace, s.DriveType);
				}

				var index = s.VolumeLabel.LastIndexOf('/');

				if (index < 1 && s.VolumeLabel.Length is 1)
				{
					return new DriveModel(s.RootDirectory.FullName, s.VolumeLabel, s.TotalSize, s.TotalSize - s.AvailableFreeSpace, s.DriveType);
				}

				return new DriveModel(s.RootDirectory.FullName, s.VolumeLabel[(index + 1)..],  s.TotalSize, s.TotalSize - s.AvailableFreeSpace, s.DriveType);
			});

	private int DriveCount => Drives.Count();
	private int FileCount => RecentFiles.Count();

	public Quickstart()
	{
		InitializeComponent();

		DataContext = this;
	}

	public ObservableRangeCollection<IFileItem>? Items { get; set; }
	public ValueTask<int> ItemCount => ValueTask.FromResult(0);

	public event Action<IFileItem>? PathChanged;
	public event Action<int>? SelectionChanged;

	public void SelectAll()
	{

	}

	public void SelectNone()
	{

	}

	public void SelectInvert()
	{

	}
}