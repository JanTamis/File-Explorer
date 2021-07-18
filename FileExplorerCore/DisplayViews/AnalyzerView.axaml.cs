using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FileExplorerCore.Helpers;
using FileExplorerCore.Models;

namespace FileExplorerCore.DisplayViews
{
	public partial class AnalyzerView : UserControl
	{
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
}
