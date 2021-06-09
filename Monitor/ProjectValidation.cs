namespace Monitor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public partial class ProjectValidation
    {
        public ProjectValidation(GitProbe gitProbe, ICollection<MonitorError> errorList)
        {
            GitProbe = gitProbe;
            ErrorList = errorList;

            GitHubApi.GitHub.ActivityReported += OnActivityReported;
        }

        public GitProbe GitProbe { get; }
        public ICollection<MonitorError> ErrorList { get; }

        public async Task Validate()
        {
            bool HasRepositoryOrSolutionChecked = false;

            foreach (RepositoryInfo Item in GitProbe.RepositoryList)
                if (!Item.IsChecked)
                {
                    await ValidateRepository(Item);
                    Item.SetChecked();
                    HasRepositoryOrSolutionChecked = true;
                }

            foreach (SolutionInfo Item in GitProbe.SolutionList)
                if (!Item.IsChecked)
                {
                    await ValidateSolution(Item);
                    Item.SetChecked();
                    HasRepositoryOrSolutionChecked = true;
                }

            if (HasRepositoryOrSolutionChecked)
            {
                List<string> ShortNameList = GetPackageShortNameList();
                ValidatePackageReferenceVersions();
                ValidatePackageReferenceConditions(ShortNameList);
            }

            TagValidRepositories();

            GitHubApi.GitHub.SubscribeToActivity();
        }

        private static bool IsContentEqual(byte[] content1, byte[] content2)
        {
            int i1, i2;
            for (i1 = 0, i2 = 0; i1 < content1.Length && i2 < content2.Length; i1++, i2++)
            {
                byte c1 = content1[i1];
                byte c2 = content2[i2];

                if (c1 == 0x0D && i1 + 1 < content1.Length)
                    c1 = content1[++i1];

                if (c2 == 0x0D && i2 + 1 < content2.Length)
                    c2 = content2[++i2];

                if (c1 != c2)
                    return false;
            }

            return true;
        }

        private void TagValidRepositories()
        {
            foreach (RepositoryInfo Repository in GitProbe.RepositoryList)
                if (Repository.IsValid)
                    GitProbe.TagValidRepository(Repository);
        }

        private void AddErrorIfNewOnly(RepositoryInfo repository, string errorText)
        {
            foreach (MonitorError Error in ErrorList)
                if (Error is RepositoryError AsRepositoryError && AsRepositoryError.Repository == repository && AsRepositoryError.ErrorText == errorText)
                    return;

            ErrorList.Add(new RepositoryError(repository, errorText));
        }

        private void OnActivityReported(object sender, GitHubApi.ActivityReportedEventArgs args)
        {
            List<long> RepositoryIdList = args.ModifiedRepositoryIdList;
            List<MonitorError> ErrorToRemoveList = new();

            if (RepositoryIdList.Count > 0)
            {
                GitHubApi.GitHub.UnsubscribeToActivity();
                GitHubApi.GitHub.ClearCache();

                RemovePackageErrors(ErrorToRemoveList);
                RemoveRepositoriesErrors(RepositoryIdList, ErrorToRemoveList);
                TagRepositoriesUnchecked(RepositoryIdList);
            }

            NotifyActivityReported(ErrorToRemoveList);
        }

        private void RemovePackageErrors(List<MonitorError> errorToRemoveList)
        {
            foreach (MonitorError Error in ErrorList)
                if (Error is PackageError)
                    errorToRemoveList.Add(Error);
        }

        private void RemoveRepositoriesErrors(List<long> repositoryIdList, List<MonitorError> errorToRemoveList)
        {
            foreach (long Id in repositoryIdList)
            {
                foreach (MonitorError Error in ErrorList)
                    if (Error is RepositoryError AsRepositoryError && AsRepositoryError.Repository.Id == Id)
                        errorToRemoveList.Add(Error);
            }
        }

        private void TagRepositoriesUnchecked(List<long> repositoryIdList)
        {
            foreach (long Id in repositoryIdList)
            {
                foreach (RepositoryInfo Repository in GitProbe.RepositoryList)
                    if (Repository.Id == Id)
                    {
                        Repository.ResetChecked();
                        break;
                    }
            }
        }

        public event EventHandler<ActivityReportedArgs>? ActivityReported;

        private void NotifyActivityReported(List<MonitorError> errorToRemoveList)
        {
            ActivityReported?.Invoke(null, new ActivityReportedArgs(errorToRemoveList));
        }
    }
}
