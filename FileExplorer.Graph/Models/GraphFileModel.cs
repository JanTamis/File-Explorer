using FileExplorer.Core.Interfaces;
using Microsoft.Graph;

namespace FileExplorer.Graph.Models
{
	public sealed class GraphFileModel : IFileItem
	{
		internal readonly DriveItem item;

		public bool IsSelected { get; set; }

		public bool IsFolder => item.Folder != null;

		public bool IsRoot => false;

		public string Extension => IsFolder ? Path.GetExtension(Name) : String.Empty;

		public string Name
		{
			get => item.Name;
			set => item.Name = value;
		}

		public long Size => item.Size.GetValueOrDefault();

		public DateTime EditedOn => item.LastModifiedDateTime.GetValueOrDefault().DateTime;

		public bool IsVisible { get; set; }

		public GraphFileModel(DriveItem item)
		{
			this.item = item;
		}

		public T GetPath<T>(ReadOnlySpanFunc<char, T> action)
		{
			return action(Name);
		}

		public T GetPath<T, TParameter>(ReadOnlySpanFunc<char, TParameter, T> action, TParameter parameter)
		{
			return action(Name, parameter);
		}

		public override int GetHashCode()
		{
			return item.Id.GetHashCode();
		}

		public override string ToString()
		{
			return Name;
		}
	}
}