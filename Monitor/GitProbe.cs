namespace Monitor
{
    using System;
    using System.Threading.Tasks;

    public partial class GitProbe
    {
        #region Init
        public GitProbe(string owner, RepositoryInfoCollection repositoryList, SolutionInfoCollection solutionList, ProjectInfoCollection projectList)
        {
            Owner = owner;
            RepositoryList = repositoryList;
            SolutionList = solutionList;
            ProjectList = projectList;
        }
        #endregion

        #region Properties
        public string ApplicationName { get; } = "Repo-Inspector";
        public string Owner { get; }
        public bool IsConnected { get; private set; }
        public RepositoryInfoCollection RepositoryList { get; }
        public SolutionInfoCollection SolutionList { get; }
        public ProjectInfoCollection ProjectList { get; }
        public double RemaingingRequests { get; private set; }
        #endregion

        #region Client Interface
        public async Task Start()
        {
            IsConnected = await Init();
            NotifyConnected();

            if (!IsConnected)
                return;

            await EnumerateRepositories();
            await EnumerateBranches();
            await EnumerateSolutions();
            await UpdateRemaingingRequests();
        }

        public void Stop()
        {
            RepositoryList.Clear();
            SolutionList.Clear();
            ProjectList.Clear();
        }
        #endregion

        #region Events
        public event EventHandler? Connected;

        protected void NotifyConnected()
        {
            Connected?.Invoke(this, EventArgs.Empty);
        }
        #endregion
    }
}
