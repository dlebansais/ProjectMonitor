namespace Monitor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using RegistryTools;

    public partial class GitProbe
    {
        public void Init()
        {
            using Stream ResourceStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Monitor.Token.txt");
            using StreamReader ResourceReader = new(ResourceStream, Encoding.ASCII);
            string Token = ResourceReader.ReadToEnd();

            GitHubApi.GitHubSettings.Token = Token;
            GitHubApi.GitHubSettings.OwnerName = OwnerName;
            RepositorySettings = new("ProjectMonitor", "Repositories");
        }

        public async Task EnumerateRepositories()
        {
            Dictionary<GitHubApi.GitHubRepository, DateTime> ProcessedTable = new();
            List<GitHubApi.GitHubRepository> UnprocessedList = new();
            await EnumerateProcessedAndUnprocessedRepositories(ProcessedTable, UnprocessedList);

            SortEnumeratedRepositories(ProcessedTable, UnprocessedList);
        }

        public async Task EnumerateProcessedAndUnprocessedRepositories(Dictionary<GitHubApi.GitHubRepository, DateTime> processedTable, List<GitHubApi.GitHubRepository> unprocessedList)
        {
            List<GitHubApi.GitHubRepository> ResultItems = await GitHubApi.GitHub.EnumerateRepositories();
            NotifyStatusUpdated();

            foreach (GitHubApi.GitHubRepository Item in ResultItems)
            {
                string LastCheckDateKey = SettingLastCheckDateKey(Item);
                if (RepositorySettings.GetString(LastCheckDateKey, string.Empty, out string Value) && long.TryParse(Value, out long FileTime))
                    processedTable.Add(Item, DateTime.FromFileTimeUtc(FileTime));
                else
                    unprocessedList.Add(Item);
            }
        }

        public void SortEnumeratedRepositories(Dictionary<GitHubApi.GitHubRepository, DateTime> processedTable, List<GitHubApi.GitHubRepository> unprocessedList)
        {
            GitHubApi.GitHubRepository? OldestRepository;

            do
            {
                OldestRepository = null;

                if (unprocessedList.Count > 0)
                {
                    OldestRepository = unprocessedList.First();
                    unprocessedList.RemoveAt(0);
                }
                else
                {
                    foreach (KeyValuePair<GitHubApi.GitHubRepository, DateTime> Entry in processedTable)
                        if (OldestRepository == null || Entry.Value < processedTable[OldestRepository])
                            OldestRepository = Entry.Key;

                    if (OldestRepository != null)
                        processedTable.Remove(OldestRepository);
                }

                if (OldestRepository != null)
                    RepositoryList.Add(new RepositoryInfo(RepositoryList, OldestRepository));
            }
            while (OldestRepository != null);
        }

        public async Task EnumerateBranches()
        {
            foreach (RepositoryInfo Repository in RepositoryList)
            {
                if (Repository.IsChecked)
                    continue;

                List<GitHubApi.GitHubBranch> BranchItems = await GitHubApi.GitHub.EnumerateBranches(Repository.Source);
                NotifyStatusUpdated();

                Repository.BranchList.Clear();

                foreach (GitHubApi.GitHubBranch Item in BranchItems)
                {
                    BranchInfo NewBranch = new BranchInfo(Repository, Item);
                    Repository.BranchList.Add(NewBranch);
                }

                Repository.CheckMasterBranch();
            }
        }

        public async Task EnumerateSolutions()
        {
            foreach (RepositoryInfo Repository in RepositoryList)
            {
                if (Repository.IsChecked)
                    continue;

                List<SolutionInfo> SolutionToRemoveList = new();
                foreach (SolutionInfo Item in SolutionList)
                    if (Item.ParentRepository == Repository)
                        SolutionToRemoveList.Add(Item);

                List<ProjectInfo> ProjectToRemoveList = new();
                foreach (ProjectInfo Item in ProjectList)
                    if (Item.ParentSolution.ParentRepository == Repository)
                        ProjectToRemoveList.Add(Item);

                foreach (SolutionInfo Item in SolutionToRemoveList)
                    SolutionList.Remove(Item);

                foreach (ProjectInfo Item in ProjectToRemoveList)
                    ProjectList.Remove(Item);

                await EnumerateSolutionsInRepository(Repository);
            }
        }

        public async Task EnumerateSolutionsInRepository(RepositoryInfo repository)
        {
            Dictionary<string, Stream?> SolutionStreamTable = await GitHubApi.GitHub.EnumerateFiles(repository.Source, "/", ".sln");
            NotifyStatusUpdated();

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
                            byte[]? Content = await GitHubApi.GitHub.DownloadFile(repository.Source, ProjectItem.RelativePath);
                            NotifyStatusUpdated();

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
                        SolutionInfo NewSolution = new(SolutionList, repository, Solution, LoadedProjectList);
                        SolutionList.Add(NewSolution);

                        foreach (ProjectInfo Item in NewSolution.ProjectList)
                            Item.ParentSolution = NewSolution;

                        repository.SolutionList.Add(NewSolution);

                        IsMainProjectExe |= CheckMainProjectExe(NewSolution);
                    }
                }

            repository.IsMainProjectExe = IsMainProjectExe;
        }

        private bool CheckMainProjectExe(SolutionInfo solution)
        {
            foreach (ProjectInfo Item in solution.ProjectList)
                if (!Item.RelativePath.StartsWith("Test\\"))
                    if (Item.ProjectType == SlnExplorer.ProjectType.Console || Item.ProjectType == SlnExplorer.ProjectType.WinExe)
                        return true;

            return false;
        }

        public void TagValidRepository(RepositoryInfo repository)
        {
            DateTime TimeNow = DateTime.UtcNow;
            long FileTime = TimeNow.ToFileTime();

            string LastCheckDateKey = SettingLastCheckDateKey(repository.Source);
            RepositorySettings.SetString(LastCheckDateKey, FileTime.ToString());

            string LastValidCommitKey = SettingLastValidCommitKey(repository.Source);
            RepositorySettings.SetString(LastValidCommitKey, repository.MasterCommitSha);
        }

        private static string SettingLastCheckDateKey(GitHubApi.GitHubRepository repository)
        {
            return $"{repository.Name}-LastCheckDate";
        }

        private static string SettingLastValidCommitKey(GitHubApi.GitHubRepository repository)
        {
            return $"{repository.Name}-LastValidCommit";
        }

        public bool IsKnownAsValid(RepositoryInfo repository)
        {
            string LastValidCommitKey = SettingLastValidCommitKey(repository.Source);
            string LastValidCommit = RepositorySettings.GetString(LastValidCommitKey, string.Empty);

            return LastValidCommit == repository.MasterCommitSha;
        }

        private Settings RepositorySettings = null!;
    }
}
