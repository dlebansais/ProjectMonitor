namespace Monitor
{
    using Octokit;
    using RegistryTools;
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
            RepositorySettings = new("ProjectMonitor", "Repositories");

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
            Dictionary<Repository, DateTime> LastProcessList = new();
            List<Repository> ResultList = new();

            foreach (Repository Repository in Result.Items)
            {
                if (RepositorySettings.GetString(Repository.Name, string.Empty, out string Value) && long.TryParse(Value, out long FileTime))
                    LastProcessList.Add(Repository, DateTime.FromFileTimeUtc(FileTime));
                else
                    ResultList.Add(Repository);
            }

            for (int i = 0; i < 3; i++)
            {
                Repository? OldestRepository = null;

                if (ResultList.Count > 0)
                {
                    ResultList.RemoveAt(0);
                    OldestRepository = ResultList.First();
                }
                else
                {
                    foreach (KeyValuePair<Repository, DateTime> Entry in LastProcessList)
                        if (OldestRepository == null || Entry.Value < LastProcessList[OldestRepository])
                            OldestRepository = Entry.Key;

                    if (OldestRepository != null)
                        LastProcessList.Remove(OldestRepository);
                }

                if (OldestRepository != null)
                    RepositoryList.Add(new RepositoryInfo(RepositoryList, OldestRepository));
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
                Dictionary<string, Stream?> SolutionStreamTable = await SearchAndDownloadRepositoryFiles(Repository, "/", ".sln");
                bool IsMainProjectExe = false;

                foreach (KeyValuePair<string, Stream?> Entry in SolutionStreamTable)
                    if (Entry.Value != null)
                    {
                        string SolutionName = Path.GetFileNameWithoutExtension(Entry.Key);
                        Stream SolutionStream = Entry.Value;

                        using StreamReader Reader = new(SolutionStream, Encoding.UTF8);
                        SlnExplorer.Solution Solution = new(SolutionName, Reader);
                        List<ProjectInfo> LoadedProjectList = new();

                        foreach (SlnExplorer.Project ProjectItem in Solution.ProjectList)
                        {
                            bool IsIgnored = ProjectItem.ProjectType > SlnExplorer.ProjectType.KnownToBeMSBuildFormat;

                            if (!IsIgnored)
                            {
                                byte[]? Content = await DownloadRepositoryFile(Repository, ProjectItem.RelativePath);
                                if (Content != null)
                                {
                                    using MemoryStream Stream = new MemoryStream(Content);
                                    ProjectItem.LoadDetails(Stream);
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

                            foreach (ProjectInfo Item in NewSolution.ProjectList)
                                Item.ParentSolution = NewSolution;

                            Repository.SolutionList.Add(NewSolution);

                            IsMainProjectExe |= CheckMainProjectExe(NewSolution);
                        }
                    }

                Repository.IsMainProjectExe = IsMainProjectExe;
            }
        }

        public async Task<Dictionary<string, Stream?>> SearchAndDownloadRepositoryFiles(RepositoryInfo repository, string path, string searchPattern)
        {
            Debug.WriteLine($"Searching and downloading {path} {searchPattern} from {repository.Owner}/{repository.Name}");

            Dictionary<string, Stream?> Result = new();

            SearchCodeRequest Request = new SearchCodeRequest();
            Request.Path = path;
            Request.FileName = searchPattern;
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

                string FileName = Path.GetFileName(Url);
                Debug.WriteLine($"  {FileName}...");

                Stream? Stream = await HttpHelper.Download(Url);

                Result.Add(ActualFileName, Stream);
            }

            await UpdateRemaingingRequests();

            return Result;
        }

        public async Task<byte[]?> DownloadRepositoryFile(RepositoryInfo repository, string filePath)
        {
            string UpdatedFilePath = filePath.Replace("\\", "/");
            string RepositoryAddress = $"{repository.Owner}/{repository.Name}";

            Debug.WriteLine($"Downloading {RepositoryAddress} {UpdatedFilePath}");

            if (DownloadCache.ContainsKey(RepositoryAddress))
            {
                Dictionary<string, byte[]> RepositoryCache = DownloadCache[RepositoryAddress];
                if (RepositoryCache.ContainsKey(UpdatedFilePath))
                {
                    Debug.WriteLine($"  (Already downloaded)");
                    return RepositoryCache[UpdatedFilePath];
                }
            }

            byte[]? Result = null;

            try
            {
                Result = await Client.Repository.Content.GetRawContent(repository.Owner, repository.Name, UpdatedFilePath);
            }
            catch (Exception e) when (e is NotFoundException)
            {
                Debug.WriteLine("(not found)");
            }

            if (Result != null)
            {
                if (!DownloadCache.ContainsKey(RepositoryAddress))
                    DownloadCache.Add(RepositoryAddress, new Dictionary<string, byte[]>());

                Dictionary<string, byte[]> RepositoryCache = DownloadCache[RepositoryAddress];
                if (!RepositoryCache.ContainsKey(UpdatedFilePath))
                    RepositoryCache.Add(UpdatedFilePath, Result);
            }

            return Result;
        }

        private Dictionary<string, Dictionary<string, byte[]>> DownloadCache = new();

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

        public void TagValidRepository(RepositoryInfo repository)
        {
            DateTime TimeNow = DateTime.UtcNow;
            long FileTime = TimeNow.ToFileTime();

            RepositorySettings.SetString(repository.Name, FileTime.ToString());
        }

        private GitHubClient Client = null!;
        private User User = null!;
        private Settings RepositorySettings = null!;
    }
}
