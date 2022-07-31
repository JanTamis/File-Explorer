namespace FileExplorer.Core.Interfaces;

public interface IFileItem
{
	string Name { get; }
	string Extension { get; }

	long Size { get; }

	DateTime EditedOn { get; }

	bool IsSelected { get; set; }
	bool IsFolder { get; }
	bool HasChildren { get; }
	bool HasParent { get; }

	IEnumerable<IFileItem> Children { get; }

	T GetPath<T>(ReadOnlySpanFunc<char, T> action);
	T GetPath<T, TParameter>(ReadOnlySpanFunc<char, TParameter, T> action, TParameter parameter);
}

public delegate TResult ReadOnlySpanFunc<T, out TResult>(ReadOnlySpan<T> span);

public delegate TResult ReadOnlySpanFunc<T, in TParameter, out TResult>(ReadOnlySpan<T> span, TParameter parameter);