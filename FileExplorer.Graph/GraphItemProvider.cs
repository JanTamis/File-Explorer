using Azure.Identity;
using FileExplorer.Core.Interfaces;
using Microsoft.Graph;
using Microsoft.Identity.Client;

namespace FileExplorer.Graph;

public class GraphItemProvider : IItemProvider
{
	private readonly GraphServiceClient Client;

	public GraphItemProvider(Func<string, Uri, CancellationToken, Task> getCode)
	{
		var scopes = new[] { "User.Read", "Files.ReadWrite.All", "profile" };

		// Multi-tenant apps can use "common",
		// single-tenant apps must use the tenant ID from the Azure portal
		var tenantId = "common";

		// Value from app registration
		var clientId = "6d445cbd-4ec8-4bb4-b405-49d8735f5b36";

		if (OperatingSystem.IsWindows())
		{
			var pca = PublicClientApplicationBuilder
				.Create(clientId)
				.WithTenantId(tenantId)
				.Build();

			// DelegateAuthenticationProvider is a simple auth provider implementation
			// that allows you to define an async function to retrieve a token
			// Alternatively, you can create a class that implements IAuthenticationProvider
			// for more complex scenarios
			var authProvider = new DelegateAuthenticationProvider(async (request) =>
			{
				// Use Microsoft.Identity.Client to retrieve token
				var result = await pca.AcquireTokenByIntegratedWindowsAuth(scopes).ExecuteAsync();

				request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.AccessToken);
			});

			Client = new GraphServiceClient(authProvider);
		}

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
		var response = await Client.Me.Request().GetAsync();

		return response.DisplayName;
	}

	public IEnumerable<IFileItem> GetItems(string path, string filter, bool recursive)
	{
		throw new NotImplementedException();
	}

	public IEnumerable<IPathSegment> GetPath(string? path)
	{
		throw new NotImplementedException();
	}
}