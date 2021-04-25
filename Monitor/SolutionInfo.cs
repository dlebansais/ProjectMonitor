namespace Monitor
{
    using SlnExplorer;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;

    public class SolutionInfo : IStatusInfo, INotifyPropertyChanged
    {
        public SolutionInfo(IStatusInfoCollection ownerCollection, RepositoryInfo repository, Solution solution, List<Project> projectList)
        {
            OwnerCollection = ownerCollection;
            Repository = repository;
            Source = solution;
            ProjectList = projectList;
            IsValid = true;
        }

        public IStatusInfoCollection OwnerCollection { get; }
        public RepositoryInfo Repository { get; }
        public Solution Source { get; }
        public List<Project> ProjectList { get; }
        public bool IsValid { get; private set; }

        public void Invalidate()
        {
            IsValid = false;
            OwnerCollection.NotifyValidCountChanged();
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
