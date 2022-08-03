using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace FileExplorer.Helpers;

public static class SettingsHelpers
{
	public static async Task UpdateSettings<T>(T value)
	{
		await using var stream = File.OpenWrite(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"));

		await JsonSerializer.SerializeAsync(stream, value);
	}

	public static async ValueTask<T> GetSettings<T>()
	{
		await using var stream = File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"));

		return await JsonSerializer.DeserializeAsync<T>(stream);
	}
}