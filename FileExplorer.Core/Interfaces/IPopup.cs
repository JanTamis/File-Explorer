﻿namespace FileExplorer.Core.Interfaces;

public interface IPopup
{
	void Close();

	bool HasShadow { get; }
	bool HasToBeCanceled { get; }
	string Title { get; }

	event Action OnClose;
}