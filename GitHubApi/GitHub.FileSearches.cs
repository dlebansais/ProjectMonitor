namespace GitHubApi
{
    using Octokit;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;

    public static partial class GitHub
    {
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
