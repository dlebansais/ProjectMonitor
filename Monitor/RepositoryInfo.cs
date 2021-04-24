namespace Monitor
{
    using Octokit;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;

    public class RepositoryInfo : INotifyPropertyChanged
    {
        public RepositoryInfo(Repository repository)
        {
            Source = repository;
        }

        public Repository Source { get; }

        public bool Private { get { return Source.Private; } }
        public string Owner { get { return Source.Owner.Login; } }
        public string Name { get { return Source.Name; } }
        public long Id { get { return Source.Id; } }
        public ObservableCollection<BranchInfo> BranchList { get; } = new ObservableCollection<BranchInfo>();
        public BranchInfo MasterBranch { get; private set; } = null!;
        public GitReference MasterCommit { get; private set; } = new();

        public void CheckMasterBranch()
        {
            foreach (BranchInfo Branch in BranchList)
                if (Branch.Name == "master")
                {
                    MasterBranch = Branch;
                    MasterCommit = MasterBranch.Commit;
                    NotifyPropertyChanged(nameof(MasterBranch));
                    NotifyPropertyChanged(nameof(MasterCommit));
                    break;
                }
        }

        #region Implementation of INotifyPropertyChanged
        /// <summary>
        /// Implements the PropertyChanged event.
        /// </summary>
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
