namespace Monitor
{
#if NO
    using Octokit;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using SlnExplorer;

    internal class GitHub
    {
        #region Init
        public GitHub(string owner, ICollection<RepositoryInfo> repositoryList, ICollection<Solution> SolutionList, Dictionary<Solution, List<Project>> projectTable)
        {
        }
        #endregion

        #region Properties
        public string Owner { get; }
        #endregion

        public async Task Init(string owner)
        {
            Client = new GitHubClient(new ProductHeaderValue(ApplicationName));
            User = await Client.User.Get(owner);
        }

        public async Task EnumerateRepositories()
        {
            SearchRepositoriesRequest Request = new SearchRepositoriesRequest() { User = User.Login };
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
                    BranchInfo NewBranch = new BranchInfo(Branch);
                    Repository.BranchList.Add(NewBranch);
                }

                Repository.CheckMasterBranch();
            }
        }

        public async Task EnumerateSolutions()
        {
            foreach (RepositoryInfo Repository in RepositoryList)
            {
                SearchCodeRequest Request = new SearchCodeRequest();
                Request.FileName = ".sln";
                Request.Repos.Add(Repository.Owner, Repository.Name);

                SearchCodeResult Result = await Client.Search.SearchCode(Request);
                int Count = Result.TotalCount;

                foreach (SearchCode Item in Result.Items)
                {
                    string Url = Item.HtmlUrl;
                    string SearchPattern = $"github.com/{Repository.Owner}/{Repository.Name}/blob";
                    string ReplacePattern = $"raw.githubusercontent.com/{Repository.Owner}/{Repository.Name}";
                    Url = Url.Replace(SearchPattern, ReplacePattern);

                    using Stream? Stream = await HttpHelper.Download(Url);
                    if (Stream != null)
                    {
                        using StreamReader Reader = new StreamReader(Stream, Encoding.UTF8);
                        Solution NewSolution = new Solution(Item.Name, Reader);
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
        }

        public void Cleanup()
        {
            RepositoryList.Clear();
            SolutionList.Clear();
            Client = null!;
            User = null!;
        }


        private GitHubClient Client = null!;
        private User User = null!;
    }
#endif
}
