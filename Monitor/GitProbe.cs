namespace Monitor
{
    using SlnExplorer;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public partial class GitProbe
    {
        #region Init
        public GitProbe(string owner, ICollection<RepositoryInfo> repositoryList, ICollection<Solution> solutionList, Dictionary<Solution, List<Project>> projectTable)
        {
            Owner = owner;
            RepositoryList = repositoryList;
            SolutionList = solutionList;
            ProjectTable = projectTable;
        }
        #endregion

        #region Properties
        public string ApplicationName { get; } = "Repo-Inspector";
        public string Owner { get; }
        public ICollection<RepositoryInfo> RepositoryList { get; }
        public ICollection<Solution> SolutionList { get; }
        public Dictionary<Solution, List<Project>> ProjectTable { get; }
        #endregion

        #region Client Interface
        public async Task<bool> Start()
        {
            bool IsConnected = await Init();
            if (!IsConnected)
                return false;

            await EnumerateRepositories();
            await EnumerateBranches();
            await EnumerateSolutions();

            return true;
        }

        public void Stop()
        {
            RepositoryList.Clear();
            SolutionList.Clear();
            ProjectTable.Clear();
        }
        #endregion
    }
}
