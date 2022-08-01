using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FileExplorerCore.Helpers;
using FileExplorerCore.Interfaces;
using FileExplorerCore.Models;

namespace FileExplorerCore.DisplayViews;

public partial class AnalyzerView : UserControl, IFileViewer
{
	public IEnumerable<IItem> Items { get; set; }
	public Task<int> ItemCount => Task.FromResult(0);
	public event Action<string>? PathChanged;
	public event Action? SelectionChanged;

	public Action SelectAll { get; }
	public Action SelectNone { get; }
	public Action SelectInvert { get; }

	public ObservableRangeCollection<FileIndexModel> Root { get; private set; } = new();
	public ObservableRangeCollection<ExtensionModel> Extensions { get; private set; } = new();

	public AnalyzerView()
	{
		DataContext = this;
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}