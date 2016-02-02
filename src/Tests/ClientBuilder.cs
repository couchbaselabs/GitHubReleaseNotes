using System.Net.Http;
using ReleaseNotesCompiler.Tests;

namespace ReleaseNotesCompiler.Tests
{
    using Octokit;
    using Octokit.Internal;

    public static class ClientBuilder
    {
        public static GitHubClient Build()
        {
            var credentialStore = new InMemoryCredentialStore(Helper.Credentials);

            var httpClient = new HttpClientAdapter(()=>{ return new HttpClientHandler(); });

            var connection = new Connection(
                new ProductHeaderValue("ReleaseNotesCompiler"),
                GitHubClient.GitHubApiUrl,
                credentialStore,
                httpClient,
                new SimpleJsonSerializer());

            return new GitHubClient(connection);
        }
    }
}
