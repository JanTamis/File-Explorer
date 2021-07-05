using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Threading.Tasks;

namespace FileExplorerCore.Helpers
{
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

		public static bool IsValidAsync(FileSystemEntry systemEntry, IEnumerable<(Categories, (Func<object, object, bool>, object))> query)
		{
			foreach (var item in query)
			{
				var method = item.Item2.Item1;
				var value = item.Item2.Item2;

				switch (item.Item1)
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

						if (!systemEntry.IsDirectory && fileTempExtension != ".dll" && fileTempExtension != ".exe" && !systemEntry.Attributes.HasFlag(FileAttributes.Archive) && !systemEntry.Attributes.HasFlag(FileAttributes.NotContentIndexed))
						{
							string searchText = (string)value;
							string line;

							using (var reader = File.OpenText(systemEntry.ToFullPath()))
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
						break;
				}
			}
			return false;
		}

		public static IEnumerable<(Categories, (Func<object, object, bool>, object))> PrepareQuery(string pattern)
		{
			var entries = pattern.Split('|', StringSplitOptions.RemoveEmptyEntries);
			var list = new List<(Categories, (Func<object, object, bool>, object))>();

			Func<object, object, bool> function;

			foreach (var entry in entries)
			{
				var temp = entry.Split(':', StringSplitOptions.RemoveEmptyEntries);

				if (temp.Length is 2 && Enum.TryParse<Categories>(temp[0].ToLower(), out var category))
				{
					string value = temp[1];

					switch (category)
					{
						case Categories.date:
							DateTime datetime;

							foreach (var operatorValue in operators)
							{
								if (value.StartsWith(operatorValue.Key))
								{
									value = value[operatorValue.Key.Length..];

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
									list.Add((category, (operatorValue.Value, datetime.Date)));
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

							break;
						case Categories.size:
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
							break;
						case Categories.name:
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
							break;
						case Categories.ext:
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
							break;
						case Categories.@is:
							FileAttributes attribute;

							if (Enum.TryParse(value, out attribute))
							{
								if (value.StartsWith('!'))
								{
									list.Add((category, ((x, y) => false, attribute)));
								}
								else if (value.StartsWith('=') || operators.ContainsKey("="))
								{
									list.Add((category, ((x, y) => true, attribute)));
								}
							}
							break;
						case Categories.contains:


							list.Add((category, (null, value)));
							break;
					}
				}
			}

			return list;
		}
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
}