using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;

namespace FileExplorerCore.Helpers;

public static class FileSearcher
{
	static readonly Dictionary<string, Func<object, object, bool>> operators = new()
	{
		{ "<", (x, y) => ((IComparable)x).CompareTo(y) is -1 },
		{ ">", (x, y) => ((IComparable)x).CompareTo(y) is 1 },
		{ "<=", (x, y) => ((IComparable)x).CompareTo(y) is -1 or 0 },
		{ ">=", (x, y) => ((IComparable)x).CompareTo(y) is 1 or 0 },
		{ "=", (x, y) => x.Equals(y) },
		{ "!=", (x, y) => !x.Equals(y) },
		{ "!", (x, y) => !x.Equals(y) },
	};

	public static bool IsValid(FileSystemEntry systemEntry, IEnumerable<(Categories, (Func<object, object, bool>, object))> query)
	{
		foreach (var (categories, (method, value)) in query)
		{
			switch (categories)
			{
				case Categories.date:
					if (method(systemEntry.LastWriteTimeUtc.LocalDateTime.Date, value))
					{
						return true;
					}
					break;
				case Categories.size:
					if (method(systemEntry.Length, value))
					{
						return true;
					}
					break;
				case Categories.name:
					var filename = new String(systemEntry.IsDirectory ? Path.GetFileName(systemEntry.FileName) : Path.GetFileNameWithoutExtension(systemEntry.FileName));

					if (method(value, filename))
					{
						return true;
					}
					break;
				case Categories.ext:

					if (!systemEntry.IsDirectory && Path.GetExtension(systemEntry.FileName) is { } extension && extension != ReadOnlySpan<char>.Empty)
					{
						var fileExtension = new String(extension[1..]);

						if (method(fileExtension, value))
						{
							return true;
						}
					}
					break;
				case Categories.@is:
					var attribute = (FileAttributes)value;

					if (method(null, null) && systemEntry.Attributes.HasFlag(attribute))
					{
						return true;
					}
					else if (!method(null, null) && !systemEntry.Attributes.HasFlag(attribute))
					{
						return true;
					}
					break;
				case Categories.contains:
					var fileTempExtension = Path.GetExtension(systemEntry.FileName);

					if (!systemEntry.Attributes.HasFlag(FileAttributes.Directory) && !systemEntry.Attributes.HasFlag(FileAttributes.Archive) && !systemEntry.Attributes.HasFlag(FileAttributes.NotContentIndexed))
					{
						try
						{
							var searchText = (string)value;
							string line;

							using (var fileStream = File.OpenRead(systemEntry.ToFullPath()))
							{
								if (IsTextFile(fileStream, false))
								{
									using (var reader = new StreamReader(fileStream))
									{
										while ((line = reader.ReadLine()) is not null && line.Length >= searchText.Length)
										{
											if (line.Contains(searchText!, StringComparison.CurrentCultureIgnoreCase))
											{
												return true;
											}
										}
									}
								}
							}
						}
						catch (Exception) { }
					}
					break;
			}
		}
		return false;
	}

	public static IEnumerable<(Categories, (Func<object?, object?, bool>, object))> PrepareQuery(string pattern)
	{
		var entries = pattern.Split('|', StringSplitOptions.RemoveEmptyEntries);
		var list = new List<(Categories, (Func<object?, object?, bool>, object))>();

		Func<object?, object?, bool> function;

		foreach (var entry in entries)
		{
			var temp = entry.Split(':', StringSplitOptions.RemoveEmptyEntries);

			if (temp.Length is 2 && Enum.TryParse<Categories>(temp[0].ToLower(), out var category))
			{
				var value = temp[1];

				if (category == Categories.date)
				{
					DateTime datetime;

					foreach (var (key, func) in operators)
					{
						if (value.StartsWith(key))
						{
							value = value[key.Length..];

							if (!DateTime.TryParse(value, out datetime))
							{
								if (String.Equals(value, "today", StringComparison.CurrentCultureIgnoreCase))
								{
									datetime = DateTime.Today;
								}
								else if (String.Equals(value, "yesterday", StringComparison.CurrentCultureIgnoreCase))
								{
									datetime = DateTime.Today.AddDays(-1);
								}
							}

							list.Add((category, (func, datetime.Date)));
							break;
						}
					}

					if (!DateTime.TryParse(value, out datetime))
					{
						if (String.Equals(value, "today", StringComparison.CurrentCultureIgnoreCase))
						{
							datetime = DateTime.Today;
						}
						else if (String.Equals(value, "yesterday", StringComparison.CurrentCultureIgnoreCase))
						{
							datetime = DateTime.Today.AddDays(-1);
						}
					}

					if (operators.TryGetValue("=", out function!))
					{
						list.Add((category, (function, datetime.Date)));
					}
				}
				else if (category == Categories.size)
				{
					long size;

					foreach (var operatorValue in operators)
					{
						if (value.StartsWith(operatorValue.Key) && Int64.TryParse(value[operatorValue.Key.Length..], out size))
						{
							list.Add((category, (operatorValue.Value, size)));
							break;
						}
					}

					if (operators.TryGetValue("=", out function!) && Int64.TryParse(value, out size))
					{
						list.Add((category, (function, size)));
					}
				}
				else if (category == Categories.name)
				{
					foreach (var operatorValue in operators)
					{
						if (value.StartsWith(operatorValue.Key))
						{
							list.Add((category, (operatorValue.Value, value[operatorValue.Key.Length..])));
							break;
						}
					}

					if (operators.TryGetValue("=", out function!))
					{
						list.Add((category, (function, value)));
					}
				}
				else if (category == Categories.ext)
				{
					foreach (var (key, func) in operators)
					{
						if (value.StartsWith(key))
						{
							list.Add((category, (Value: func, value[key.Length..])));
							break;
						}
					}

					if (operators.TryGetValue("=", out function!))
					{
						list.Add((category, (function, value)));
					}
				}
				else if (category == Categories.@is)
				{
					FileAttributes attribute;

					if (Enum.TryParse(value, out attribute))
					{
						if (value.StartsWith('!'))
						{
							list.Add((category, ((_, _) => false, attribute)));
						}
						else if (value.StartsWith('=') || operators.ContainsKey("="))
						{
							list.Add((category, ((_, _) => true, attribute)));
						}
					}
				}
				else if (category == Categories.contains)
				{
					list.Add((category, (null, value)));
				}
			}
		}

		return list;
	}

	public static bool IsTextFile(FileStream srcFile, bool thorough)
	{
		Span<byte> b = stackalloc byte[5];

		srcFile.Read(b);

		switch (b[0])
		{
			case 0x00 when b[1] == 0x00 && b[2] == 0xFE && b[3] == 0xFF:
			// UTF-32, little-endian
			case 0xFF when b[1] == 0xFE && b[2] == 0x00 && b[3] == 0x00:
			// UTF-16, big-endian
			case 0xFE when b[1] == 0xFF:
			// UTF-16, little-endian
			case 0xFF when b[1] == 0xFE:
			// UTF-8
			case 0xEF when b[1] == 0xBB && b[2] == 0xBF:
			// UTF-7
			case 0x2b when b[1] == 0x2f && b[2] == 0x76:
				return true; // UTF-32, big-endian 
		}

		// Maybe there is a future encoding ...
		// PS: The above yields more than this - this doesn't find UTF7 ...
		if (thorough)
		{
			foreach (var ei in System.Text.Encoding.GetEncodings())
			{
				var enc = ei.GetEncoding();
				var preamble = enc.GetPreamble();
					
				if (preamble.Length == 0)
					continue;

				if (preamble.Length > b.Length)
					continue;

				return true;
			} // Next ei
		} // End if (thorough)

		srcFile.Seek(0, SeekOrigin.Begin);

		return false;
	} // End Function BomInfo 
}

public enum Categories
{
	date,
	size,
	name,
	ext,
	@is,
	contains
}