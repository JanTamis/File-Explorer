using Avalonia.Media.TextFormatting;

namespace FileExplorer.Controls;

public readonly record struct SimpleTextSource : ITextSource
{
	private readonly string _text;
	private readonly TextRunProperties _defaultProperties;

	public SimpleTextSource(string text, TextRunProperties defaultProperties)
	{
		_text = text;
		_defaultProperties = defaultProperties;
	}

	public TextRun? GetTextRun(int textSourceIndex)
	{
		if (textSourceIndex > _text.Length)
		{
			return new TextEndOfParagraph();
		}

		var runText = _text.AsMemory(textSourceIndex);

		if (runText.IsEmpty)
		{
			return new TextEndOfParagraph();
		}

		return new TextCharacters(_text, _defaultProperties);
	}
}