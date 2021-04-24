namespace Monitor
{
    using Octokit;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public partial class GitProbe
    {
        public async Task<bool> Init()
        {
            using Stream ResourceStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Monitor.Token.txt");
            using StreamReader ResourceReader = new(ResourceStream, Encoding.ASCII);
            string Token = ResourceReader.ReadToEnd();

            Credentials AuthenticationToken = new Credentials(Token);
            Client = new GitHubClient(new ProductHeaderValue(ApplicationName));
            Client.Credentials = AuthenticationToken;

            try
            {
                User = await Client.User.Get(Owner);
                return true;
            }
            catch (AuthorizationException exception)
            {
                Debug.WriteLine($"Failed to connect: {exception.Message}");
            }
            catch
            {
                throw;
            }

            return false;
        }

        public async Task EnumerateRepositories()
        {
            SearchRepositoriesRequest Request = new() { User = User.Login };
            SearchRepositoryResult Result = await Client.Search.SearchRepo(Request);

            foreach (Repository Repository in Result.Items)
            {
                RepositoryList.Add(new RepositoryInfo(Repository));
                if (RepositoryList.Count > 2)
                    break;
            }
        }

        public async Task EnumerateBranches()
        {
            foreach (RepositoryInfo Repository in RepositoryList)
            {
                IReadOnlyList<Branch> BranchList = await Client.Repository.Branch.GetAll(Repository.Id);

                foreach (Branch Branch in BranchList)
                {
                    BranchInfo NewBranch = new BranchInfo(Repository, Branch);
                    Repository.BranchList.Add(NewBranch);
                }

                Repository.CheckMasterBranch();
            }
        }

        public async Task EnumerateSolutions()
        {
            foreach (RepositoryInfo Repository in RepositoryList)
            {
                Dictionary<string, Stream?> SolutionStreamTable = await DownloadRepositoryFile(Repository, "/", ".sln");

                foreach (KeyValuePair<string, Stream?> Entry in SolutionStreamTable)
                    if (Entry.Value != null)
                    {
                        using StreamReader Reader = new(Entry.Value, Encoding.UTF8);
                        SlnExplorer.Solution NewSolution = new(Entry.Key, Reader);
                        List<SlnExplorer.Project> LoadedProjectList = new();

                        foreach (SlnExplorer.Project ProjectItem in NewSolution.ProjectList)
                        {
                            bool IsIgnored = ProjectItem.ProjectType != "Unknown" && ProjectItem.ProjectType != "KnownToBeMSBuildFormat";

                            if (!IsIgnored)
                                LoadedProjectList.Add(ProjectItem);
                        }

                        if (LoadedProjectList.Count > 0)
                        {
                            SolutionList.Add(NewSolution);
                            ProjectTable.Add(NewSolution, LoadedProjectList);
                        }
                    }
            }
        }

        public async Task<Dictionary<string, Stream?>> DownloadRepositoryFile(RepositoryInfo repository, string path, string fileName)
        {
            Debug.WriteLine($"Downloading {path}{fileName} from {repository.Owner}/{repository.Name}");

            Dictionary<string, Stream?> Result = new();

            SearchCodeRequest Request = new SearchCodeRequest();
            Request.Path = path;
            Request.FileName = fileName;
            Request.Repos.Add(repository.Owner, repository.Name);

            SearchCodeResult SearchResult = await Client.Search.SearchCode(Request);
            
            foreach (SearchCode Item in SearchResult.Items)
            {
                string Url = Item.HtmlUrl;

                int Index = Url.LastIndexOf("/");
                string ActualFileName = Url.Substring(Index + 1);

                string SearchPattern = $"github.com/{repository.Owner}/{repository.Name}/blob";
                string ReplacePattern = $"raw.githubusercontent.com/{repository.Owner}/{repository.Name}";
                Url = Url.Replace(SearchPattern, ReplacePattern);

                Thread.Sleep(TimeSpan.FromSeconds(0.5));
                Stream? Stream = await HttpHelper.Download(Url);

                Result.Add(ActualFileName, Stream);
            }

            return Result;
        }

        private GitHubClient Client = null!;
        private User User = null!;
    }
}
