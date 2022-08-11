using FileExplorer.Core.Interfaces;
using Microsoft.Graph;

namespace FileExplorer.Graph.Models
{
	public class GraphFileModel : IFileItem
	{
		private readonly DriveItem file;

		public bool IsSelected { get; set; }

		public bool IsFolder => file.Folder is not null;

		public bool IsRoot => false;

		public IEnumerable<IFileItem> Children => IsFolder && file.Children is not null ? file.Children.Select(s => new GraphFileModel(s)) : Enumerable.Empty<IFileItem>();

		public string Extension => IsFolder ? Path.GetExtension(Name) : String.Empty;

		public string Name
		{
			get => file.Name;
			set => file.Name = value;
		}

		public long Size => file.Size.GetValueOrDefault();

		public DateTime EditedOn => file.LastModifiedDateTime.GetValueOrDefault().DateTime;

		public GraphFileModel(DriveItem file)
		{
			this.file = file;
		}

		public T GetPath<T>(ReadOnlySpanFunc<char, T> action)
		{
			return action(Name);
		}

		public T GetPath<T, TParameter>(ReadOnlySpanFunc<char, TParameter, T> action, TParameter parameter)
		{
			return action(Name, parameter);
		}
	}
}
