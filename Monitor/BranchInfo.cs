namespace Monitor
{
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;

    public class BranchInfo : INotifyPropertyChanged
    {
        public BranchInfo(RepositoryInfo repository, GitHubApi.GitHubBranch branch)
        {
            Repository = repository;
            Source = branch;
        }

        public RepositoryInfo Repository { get; }
        public GitHubApi.GitHubBranch Source { get; }

        public string Name { get { return Source.Name; } }
        //public GitReference Commit { get { return Source.Commit; } }

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
