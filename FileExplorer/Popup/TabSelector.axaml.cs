using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FileExplorer.Core.Helpers;
using FileExplorer.Core.Interfaces;
using FileExplorer.ViewModels;

namespace FileExplorer.Popup;

public sealed partial class TabSelector : UserControl, IPopup, INotifyPropertyChanged
{
	private ObservableRangeCollection<TabItemViewModel>? _tabs;
	private TabItemViewModel? _currentTab;
	
	public new event PropertyChangedEventHandler? PropertyChanged = delegate { };

	public event Action<TabItemViewModel> TabSelectionChanged = delegate { };
	
	public TabItemViewModel? CurrentTab
	{
		get => _currentTab;
		set
		{
			_currentTab = value;

			if (CurrentTab is not null)
			{
				TabSelectionChanged(CurrentTab);
			}
		}
	}

	public bool HasShadow => false;
	public bool HasToBeCanceled => false;

	public string Title => "Select Tab";

	public ObservableRangeCollection<TabItemViewModel>? Tabs
	{
		get => _tabs;
		set => OnPropertyChanged(ref _tabs, value);
	}

	public event Action OnClose = delegate { };

	public TabSelector()
	{
		DataContext = this;
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public void Close()
	{
		OnClose();
	}

	private void OnPropertyChanged<T>(ref T property, T value, [CallerMemberName] string? name = null)
	{
		property = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}