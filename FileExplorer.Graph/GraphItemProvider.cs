using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Azure.Identity;
using FileExplorer.Core.Interfaces;
using FileExplorer.Graph.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Graph;

namespace FileExplorer.Graph;

public sealed class GraphItemProvider : IItemProvider
{
	private readonly GraphServiceClient _client;
	private readonly MemoryCache _imageCache;

	public GraphItemProvider(Func<string, Uri, CancellationToken, Task> getCode)
	{
		_imageCache = new MemoryCache(new MemoryCacheOptions
		{
			ExpirationScanFrequency = TimeSpan.FromMinutes(1),
			TrackStatistics = true,
			SizeLimit = 536_870_912,
		});

		string[] scopes = ["User.Read", "Files.ReadWrite.All", "Files.Read", "Files.Read.All", "profile"];

		// Multi-tenant apps can use "common",
		// single-tenant apps must use the tenant ID from the Azure portal
		const string tenantId = "common";

		// Value from app registration
		const string clientId = "6d445cbd-4ec8-4bb4-b405-49d8735f5b36";

		// using Azure.Identity;
		var options = new TokenCredentialOptions
		{
			AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
		};

		// https://docs.microsoft.com/dotnet/api/azure.identity.devicecodecredential
		var deviceCodeCredential = new DeviceCodeCredential(Callback, tenantId, clientId, options);

		_client = new GraphServiceClient(deviceCodeCredential, scopes);

		// Callback function that receives the user prompt
		// Prompt contains the generated device code that you must
		// enter during the auth process in the browser
		Task Callback(DeviceCodeInfo code, CancellationToken cancellation)
		{
			return getCode(code.UserCode, code.VerificationUri, cancellation);
		}
	}

	public async Task<string> GetNameAsync()
	{
		var user = await _client.Me
			.Request()
			.Select(s => s.DisplayName)
			.GetAsync();

		return user.DisplayName;
	}

	public async Task<IFileItem> GetRootAsync()
	{
		var item = await _client.Me.Drive.Root
			.Request()
			.GetAsync();

		return new GraphFileModel(item);
	}

	public async ValueTask<IFileItem?> GetParentAsync(IFileItem folder, CancellationToken token = default)
	{
		if (folder is GraphFileModel { item.ParentReference: not null } fileItem)
		{
			return new GraphFileModel(await _client.Me.Drives[fileItem.item.ParentReference.DriveId].Items[fileItem.item.ParentReference.Id].Request().GetAsync(token));
		}

		return await new ValueTask<IFileItem?>(null as IFileItem);
	}

	public async IAsyncEnumerable<IFileItem> GetItemsAsync(IFileItem folder, string filter, bool recursive, [EnumeratorCancellation] CancellationToken token)
	{
		if (folder is GraphFileModel model)
		{
			ICollectionPage<DriveItem>? driveItem = recursive
				? await _client.Me.Drive.Items[model.item.Id]
					.Search(filter)
					.Request()
					.Expand("thumbnails")
					.GetAsync(token)
				: await _client.Me.Drive.Items[model.item.Id].Children
					.Request()
					.Expand("thumbnails")
					.GetAsync(token);

			while (driveItem is { CurrentPage.Count: > 0 })
			{
				foreach (var item in driveItem.CurrentPage)
				{
					if (token.IsCancellationRequested)
					{
						yield break;
					}

					yield return new GraphFileModel(item);
				}

				driveItem = driveItem switch
				{
					IDriveSearchCollectionPage { NextPageRequest: { } request } => await request.Expand("thumbnails").GetAsync(token),
					IDriveItemChildrenCollectionPage { NextPageRequest: { } request } => await request.Expand("thumbnails").GetAsync(token),
					_ => null,
				};
			}
		}
	}

	public IEnumerable<IFileItem> GetItems(IFileItem folder, string filter, bool recursive, CancellationToken token)
	{
		if (folder is GraphFileModel model)
		{
			ICollectionPage<DriveItem>? driveItem;

			if (recursive)
			{
				var resultTask = _client.Me.Drive.Items[model.item.Id]
					.Search(filter)
					.Request()
					.Expand("thumbnails")
					.GetAsync(token);

				resultTask.Wait(token);

				driveItem = resultTask.Result;
			}
			else
			{
				var resultTask = _client.Me.Drive.Items[model.item.Id].Children
					.Request()
					.Expand("thumbnails")
					.GetAsync(token);

				resultTask.Wait(token);

				driveItem = resultTask.Result;
			}

			while (driveItem is { CurrentPage.Count: > 0 } && !token.IsCancellationRequested)
			{
				foreach (var item in driveItem.CurrentPage)
				{
					if (token.IsCancellationRequested)
					{
						yield break;
					}

					yield return new GraphFileModel(item);
				}

				switch (driveItem)
				{
					case IDriveSearchCollectionPage { NextPageRequest: { } request }:
						var searchResult = request
							.GetAsync(token);

						searchResult.Wait(token);

						driveItem = searchResult.Result;
						break;
					case IDriveItemChildrenCollectionPage { NextPageRequest: { } request }:
						var childrenResult = request
							.GetAsync(token);

						childrenResult.Wait(token);

						driveItem = childrenResult.Result;
						break;
					default:
						driveItem = null;
						break;
				}
			}
		}
	}

	public bool HasItems(IFileItem folder)
	{
		return folder is GraphFileModel { item.Folder.ChildCount: > 0 };
	}

	public async ValueTask<IEnumerable<IPathSegment>> GetPathAsync(IFileItem folder)
	{
		if (folder is GraphFileModel model)
		{
			return await GetSegmentsAsync(model).Reverse().ToArrayAsync();
		}

		return Enumerable.Empty<IPathSegment>();

		async IAsyncEnumerable<IPathSegment> GetSegmentsAsync(GraphFileModel model)
		{
			yield return new GraphPathSegment(model);

			while (await GetParentAsync(model) is GraphFileModel parent)
			{
				model = parent;
				yield return new GraphPathSegment(model);
			}
		}
	}

	public Task<IImage?> GetThumbnailAsync(IFileItem? item, int size, CancellationToken token)
	{
		if (item is GraphFileModel model)
		{
			return _imageCache.GetOrCreateAsync(item.GetHashCode(), async entry =>
			{
				var thumbnail = await _client.Me.Drive.Items[model.item.Id].Thumbnails["0"]["medium"].Content
					.Request()
					.GetAsync(token);

				//using var stream = new MemoryStream(await _httpClient.GetByteArrayAsync(thumbnail.Small.Url, token), false);

				entry.SetSize(thumbnail.Length);

				return new Bitmap(thumbnail) as IImage;
			});
		}

		return Task.FromResult<IImage?>(null);
	}

	public IFolderUpdateNotificator? GetNotificator(IFileItem folder, string filter, bool recursive)
	{
		return null;
	}

	public Task EnumerateItemsAsync(IFileItem folder, string pattern, Action<IFileItem> action, CancellationToken token)
	{
		return Task.CompletedTask;
	}

	public Task EnumerateItemsAsync<T>(IFileItem folder, string pattern, Action<IEnumerable<T>> action, Func<IFileItem, T> transformation, CancellationToken token)
	{
		return Task.CompletedTask;
	}
}