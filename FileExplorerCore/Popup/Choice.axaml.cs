using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FileExplorerCore.Interfaces;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FileExplorerCore.Popup
{
	public partial class Choice : UserControl, IPopup, INotifyPropertyChanged
	{
		private string _submitText;
		private string _closeText;
		private string _message;

		public bool HasShadow => true;
		public bool HasToBeCanceled => false;


		public string SubmitText
		{
			get => _submitText;
			set => OnPropertyChanged(ref _submitText, value);
		}

		public string CloseText
		{
			get => _closeText;
			set => OnPropertyChanged(ref _closeText, value);
		}

		public string Message
		{
			get => _message;
			set
			{
				_message = value;
				OnPropertyChanged(nameof(Title));
			}
		}

		public string Title => Message;

		public event Action OnClose = delegate { };
		public event Action OnSubmit = delegate { };
		public new event PropertyChangedEventHandler PropertyChanged = delegate { };

		public Choice()
		{
			InitializeComponent();
		}

		public void Confirm()
		{
			OnSubmit();
		}

		public void Close()
		{
			OnClose();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
			DataContext = this;
		}

		protected void OnPropertyChanged<T>(ref T property, T value, [CallerMemberName] string name = null)
		{
			property = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		public void OnPropertyChanged(string name)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
	}
}
