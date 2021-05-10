namespace Monitor
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;

    public class RepositoryInfo : IStatusInfo, INotifyPropertyChanged
    {
        public RepositoryInfo(IStatusInfoCollection ownerCollection, GitHubApi.GitHubRepository repository)
        {
            OwnerCollection = ownerCollection;
            Source = repository;
            IsValid = true;
        }

        public IStatusInfoCollection OwnerCollection { get; }
        public GitHubApi.GitHubRepository Source { get; }

        public string Owner { get { return Source.Owner; } }
        public string Name { get { return Source.Name; } }
        public long Id { get { return Source.Id; } }
        public bool IsPrivate { get { return Source.IsPrivate; } }
        public ObservableCollection<BranchInfo> BranchList { get; } = new ObservableCollection<BranchInfo>();
        public GitHubApi.GitHubBranch MasterBranch { get; private set; } = null!;
        public GitHubApi.GitHubCommit? MasterCommit { get; private set; } = null;
        public string MasterCommitSha { get; private set; } = string.Empty;
        public bool IsValid { get; private set; }
        public bool IsMainProjectExe { get; set; }
        public List<SolutionInfo> SolutionList { get; } = new();
        public bool IsChecked { get; private set; }

        public void CheckMasterBranch()
        {
            if (Source.MasterCommit != null)
            {
                MasterBranch = Source.MasterCommit.Branch;
                MasterCommit = Source.MasterCommit;
                MasterCommitSha = Source.MasterCommit.Sha;

                NotifyPropertyChanged(nameof(MasterBranch));
                NotifyPropertyChanged(nameof(MasterCommit));
                NotifyPropertyChanged(nameof(MasterCommitSha));
            }
        }

        public void ResetChecked()
        {
            IsChecked = false;
            IsValid = true;
        }

        public void SetChecked()
        {
            IsChecked = true;
        }

        public void Invalidate()
        {
            IsValid = false;
            OwnerCollection.NotifyValidCountChanged();
        }

        #region Implementation of INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        internal void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification = "Default parameter is mandatory with [CallerMemberName]")]
        internal void NotifyThisPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
