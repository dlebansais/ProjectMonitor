namespace Monitor
{
    using Octokit;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
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
                RepositoryList.Add(new RepositoryInfo(RepositoryList, Repository));
                if (RepositoryList.Count > 0)
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
                bool IsMainProjectExe = false;

                foreach (KeyValuePair<string, Stream?> Entry in SolutionStreamTable)
                    if (Entry.Value != null)
                    {
                        using StreamReader Reader = new(Entry.Value, Encoding.UTF8);
                        SlnExplorer.Solution Solution = new(Entry.Key, Reader);
                        List<ProjectInfo> LoadedProjectList = new();

                        foreach (SlnExplorer.Project ProjectItem in Solution.ProjectList)
                        {
                            bool IsIgnored = ProjectItem.ProjectType > SlnExplorer.ProjectType.KnownToBeMSBuildFormat;

                            if (!IsIgnored)
                            {
                                string RelativePath = Path.GetDirectoryName(ProjectItem.RelativePath).Replace("\\", "/");
                                string ProjectFileName = Path.GetFileName(ProjectItem.RelativePath);

                                Dictionary<string, Stream?> ProjectStreamTable = await DownloadRepositoryFile(Repository, RelativePath, ProjectFileName);
                                if (ProjectStreamTable.Count > 0)
                                {
                                    KeyValuePair<string, Stream?> StreamEntry = ProjectStreamTable.First();
                                    Stream? ProjectStream = StreamEntry.Value;

                                    if (ProjectStream != null)
                                        ProjectItem.LoadDetails(ProjectStream);
                                }

                                ProjectInfo NewProject = new(ProjectList, ProjectItem);
                                ProjectList.Add(NewProject);
                                LoadedProjectList.Add(NewProject);
                            }
                        }

                        if (LoadedProjectList.Count > 0)
                        {
                            SolutionInfo NewSolution = new(SolutionList, Repository, Solution, LoadedProjectList);
                            SolutionList.Add(NewSolution);

                            IsMainProjectExe |= CheckMainProjectExe(NewSolution);
                        }
                    }

                Repository.IsMainProjectExe = IsMainProjectExe;
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

                Stream? Stream = await HttpHelper.Download(Url);

                Result.Add(ActualFileName, Stream);
            }

            await UpdateRemaingingRequests();

            return Result;
        }

        private bool CheckMainProjectExe(SolutionInfo solution)
        {
            foreach (ProjectInfo Item in solution.ProjectList)
                if (!Item.RelativePath.StartsWith("Test\\"))
                    if (Item.ProjectType == SlnExplorer.ProjectType.Console || Item.ProjectType == SlnExplorer.ProjectType.WinExe)
                        return true;

            return false;
        }

        private async Task UpdateRemaingingRequests()
        {
            if (RepositoryList.Count == 0)
                return;

            MiscellaneousRateLimit RateLimits = await Client.Miscellaneous.GetRateLimits();
            RateLimit CoreRateLimit = RateLimits.Resources.Core;
            RateLimit SearchRateLimit = RateLimits.Resources.Search;

            double CoreRatio = (1.0 * CoreRateLimit.Remaining) / CoreRateLimit.Limit;
            double SearchRatio = (1.0 * SearchRateLimit.Remaining) / SearchRateLimit.Limit;
            RemaingingRequests = Math.Max(CoreRatio, SearchRatio);
        }

        private GitHubClient Client = null!;
        private User User = null!;
    }
}
