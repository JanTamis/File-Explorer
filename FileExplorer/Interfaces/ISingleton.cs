namespace FileExplorer.Interfaces;

public interface ISingleton<out T>
{
	abstract static T Instance { get; }
}