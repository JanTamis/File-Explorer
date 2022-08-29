using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Azure.Identity;
using FileExplorer.Core.Interfaces;
using FileExplorer.Graph.Models;
using Microsoft.Graph;

namespace FileExplorer.Graph;

public class GraphItemProvider : IItemProvider
{
	private readonly GraphServiceClient Client;

	private readonly HttpClient _httpClient;

	public GraphItemProvider(Func<string, Uri, CancellationToken, Task> getCode)
	{
		_httpClient = new HttpClient();

		var scopes = new[] { "User.Read", "Files.ReadWrite.All", "Files.Read", "Files.Read.All", "profile" };

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

		Client = new GraphServiceClient(deviceCodeCredential, scopes);

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
		var user = await Client.Me.Request().GetAsync();

		return user.DisplayName;
	}

	public async Task<IFileItem> GetRootAsync()
	{
		var item = await Client.Me.Drive.Root.Request().GetAsync();

		return new GraphFileModel(item);
	}

	public async ValueTask<IFileItem?> GetParentAsync(IFileItem folder, CancellationToken token = default)
	{
		if (folder is GraphFileModel { item.ParentReference: not null } fileItem)
		{
			return new GraphFileModel(await Client.Me.Drives[fileItem.item.ParentReference.DriveId].Items[fileItem.item.ParentReference.Id].Request().GetAsync(token));
		}

		return await new ValueTask<IFileItem?>(null as IFileItem);
	}

	public async IAsyncEnumerable<IFileItem> GetItemsAsync(IFileItem folder, string filter, bool recursive, [EnumeratorCancellation] CancellationToken token)
	{
		if (folder is GraphFileModel model)
		{
			ICollectionPage<DriveItem>? driveItem = recursive
				? await Client.Me.Drive.Items[model.item.Id].Search(filter).Request().Expand("thumbnails").GetAsync()
				: await Client.Me.Drive.Items[model.item.Id].Children.Request().Expand("thumbnails").GetAsync();

			while (driveItem is { CurrentPage.Count: > 0 })
			{
				foreach (var item in driveItem.CurrentPage)
				{
					yield return new GraphFileModel(item);
				}

				driveItem = driveItem switch
				{
					IDriveSearchCollectionPage { NextPageRequest: { } request } => await request.Expand("thumbnails").GetAsync(),
					IDriveItemChildrenCollectionPage { NextPageRequest: { } request } => await request.Expand("thumbnails").GetAsync(),
					_ => null,
				};
			}
		}
	}

	public IEnumerable<IFileItem> GetItems(IFileItem folder, string filter, bool recursive, CancellationToken token)
	{
		if (folder is GraphFileModel model)
		{
			if (recursive)
			{
				var driveItem = Client.Me.Drive.Items[model.item.Id].Search(filter).Request().Expand("thumbnails").GetAsync();

				do
				{
					driveItem.Wait(token);

					if (driveItem?.Result is { CurrentPage.Count: > 0 })
					{
						foreach (var item in driveItem.Result.CurrentPage)
						{
							yield return new GraphFileModel(item);
						}
					}

					driveItem = driveItem?.Result switch
					{
						{ NextPageRequest: { } request } => request.Expand("thumbnails").GetAsync(),
						_ => null,
					};
				} while (driveItem is not null);
			}
			else
			{
				var driveItem = Client.Me.Drive.Items[model.item.Id].Children.Request().Expand("thumbnails").GetAsync();

				do
				{
					driveItem.Wait(token);

					if (driveItem?.Result is { CurrentPage.Count: > 0 })
					{
						foreach (var item in driveItem.Result.CurrentPage)
						{
							yield return new GraphFileModel(item);
						}
					}

					driveItem = driveItem?.Result switch
					{
						{ NextPageRequest: { } request } => request.Expand("thumbnails").GetAsync(),
						_ => null,
					};
				} while (driveItem is not null);
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

	public async Task<IImage?> GetThumbnailAsync(IFileItem item, int size, CancellationToken token)
	{
		if (item is GraphFileModel { item.Thumbnails: [var thumbnail, ..] })
		{
			using var stream = new MemoryStream(await _httpClient.GetByteArrayAsync(thumbnail.Small.Url, token), false);

			return new Bitmap(stream);
		}

		return await Task.FromResult(null as IImage);
	}
}