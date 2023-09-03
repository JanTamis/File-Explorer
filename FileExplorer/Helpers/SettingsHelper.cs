using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FileExplorer.Models;
using Microsoft.Graph;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace FileExplorer.Helpers;

public static class SettingsHelpers
{
	public async static Task UpdateSettings(AppSettings value)
	{
		await using var stream = File.OpenWrite(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"));

		await JsonSerializer.SerializeAsync(stream, value, AppSettingsJsonSerializerContext.Default.AppSettings);
	}

	public async static ValueTask<AppSettings> GetSettings()
	{
		await using var stream = File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"));

		return await JsonSerializer.DeserializeAsync(stream, AppSettingsJsonSerializerContext.Default.AppSettings);
	}
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class AppSettingsJsonSerializerContext : JsonSerializerContext
{
}