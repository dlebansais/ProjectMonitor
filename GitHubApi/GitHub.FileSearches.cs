namespace GitHubApi
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using Octokit;

    /// <summary>
    /// A simple class to enumerate repositories with .NET Sdk projects.
    /// </summary>
    public static partial class GitHub
    {
        /// <summary>
        /// Enumerate files in a repository.
        /// </summary>
        /// <param name="repository">The repository.</param>
        /// <param name="path">The path where to start looking.</param>
        /// <param name="searchPattern">The search pattern.</param>
        /// <returns>A table of file names and associated stream content.</returns>
        public static async Task<Dictionary<string, Stream?>> EnumerateFiles(GitHubRepository repository, string path, string searchPattern)
        {
            Dictionary<string, Stream?> Result = new();

            if (await Connect())
            {
                string Login = repository.Source.Owner.Login;
                string Name = repository.Source.Name;

                SearchCodeRequest Request = new SearchCodeRequest() { Path = path, FileName = searchPattern };
                Request.Repos.Add(Login, Name);
                SearchCodeResult SearchResult = await Client.Search.SearchCode(Request);

                await GetFileStreams(Login, Name, SearchResult, Result);
            }

            return Result;
        }

        private static async Task GetFileStreams(string login, string name, SearchCodeResult searchResult, Dictionary<string, Stream?> streamTable)
        {
            foreach (SearchCode Item in searchResult.Items)
            {
                string Url = Item.HtmlUrl;

                int Index = Url.LastIndexOf("/");
                string ActualFileName = Url.Substring(Index + 1);

                string SearchPattern = $"github.com/{login}/{name}/blob";
                string ReplacePattern = $"raw.githubusercontent.com/{login}/{name}";
                Url = Url.Replace(SearchPattern, ReplacePattern);

                string FileName = Path.GetFileName(Url);
                Debug.WriteLine($"  {FileName}...");

                Stream? Stream = await HttpHelper.Download(Url);

                streamTable.Add(ActualFileName, Stream);
            }
        }
    }
}
