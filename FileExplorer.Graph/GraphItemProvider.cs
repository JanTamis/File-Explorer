using Azure.Identity;
using FileExplorer.Core.Interfaces;
using FileExplorer.Graph.Models;
using Microsoft.Graph;
using Microsoft.Identity.Client;

namespace FileExplorer.Graph;

public class GraphItemProvider : IItemProvider
{
	private readonly GraphServiceClient Client;

	public GraphItemProvider(Func<string, Uri, CancellationToken, Task> getCode)
	{
		var scopes = new[] { "User.Read", "Files.ReadWrite.All", "Files.Read", "Files.Read.All", "profile" };

		// Multi-tenant apps can use "common",
		// single-tenant apps must use the tenant ID from the Azure portal
		var tenantId = "common";

		// Value from app registration
		var clientId = "6d445cbd-4ec8-4bb4-b405-49d8735f5b36";

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

	public async ValueTask<IEnumerable<IFileItem>> GetItemsAsync(string path, string filter, bool recursive, CancellationToken token)
	{
		try
		{
			var files = await Client.Me.Drive.Root.Children.Request().GetAsync(token);

			return files.Select(s => new GraphFileModel(s));
		}
		catch (Exception)
		{
			return Enumerable.Empty<IFileItem>();
		}
	}

	public IEnumerable<IPathSegment> GetPath(string? path)
	{
		return Enumerable.Empty<IPathSegment>();
	}
}