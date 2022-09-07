namespace FileExplorer.Core.Interfaces;

public interface IFileItem
{
	bool IsSelected { get; set; }
	bool IsFolder { get; }
	bool IsRoot { get; }

	string Extension { get; }
	string Name { get; set; }

	long Size { get; }

	public DateTime EditedOn { get; }

	public bool IsVisible { get; set; }

	T GetPath<T>(ReadOnlySpanFunc<char, T> action);
	T GetPath<T, TParameter>(ReadOnlySpanFunc<char, TParameter, T> action, TParameter parameter);
}

public delegate T ReadOnlySpanFunc<TSpan, out T>(ReadOnlySpan<TSpan> data);
public delegate T ReadOnlySpanFunc<TSpan, in TParameter, out T>(ReadOnlySpan<TSpan> data, TParameter parameter);