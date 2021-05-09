namespace Monitor
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public partial class GitProbe
    {
        #region Init
        public GitProbe(string ownerName, RepositoryInfoCollection repositoryList, SolutionInfoCollection solutionList, ProjectInfoCollection projectList)
        {
            OwnerName = ownerName;
            RepositoryList = repositoryList;
            SolutionList = solutionList;
            ProjectList = projectList;

            SlowDownTimer = new Timer(new TimerCallback(SlowDownTimerCallback), this, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
        #endregion

        #region Properties
        public string ApplicationName { get; } = "Repo-Inspector";
        public string OwnerName { get; }
        public bool IsConnected { get { return GitHubApi.GitHub.IsConnected; } }
        public bool IsSlowingDown { get { return GitHubApi.GitHub.IsSlowingDown; } }
        public RepositoryInfoCollection RepositoryList { get; }
        public SolutionInfoCollection SolutionList { get; }
        public ProjectInfoCollection ProjectList { get; }
        public double RemainingRequests { get { return GitHubApi.GitHub.RemainingRequests; } }
        #endregion

        #region Client Interface
        public async Task Start()
        {
            Init();
            await EnumerateRepositories();
            await EnumerateBranches();
            await EnumerateSolutions();
        }

        public void Stop()
        {
            RepositoryList.Clear();
            SolutionList.Clear();
            ProjectList.Clear();
        }
        #endregion

        #region Events
        public event EventHandler? StatusUpdated;
        public event EventHandler? SlowDownChanged;

        protected void NotifyStatusUpdated()
        {
            StatusUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void SlowDownTimerCallback(object parameter)
        {
            if (IsSlowingDownOld != IsSlowingDown)
            {
                IsSlowingDownOld = IsSlowingDown;
                SlowDownChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private Timer SlowDownTimer;
        private bool IsSlowingDownOld;
        #endregion
    }
}
