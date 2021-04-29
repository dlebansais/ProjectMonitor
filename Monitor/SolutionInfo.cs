namespace Monitor
{
    using SlnExplorer;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;

    [DebuggerDisplay("{Name}")]
    public class SolutionInfo : IStatusInfo, INotifyPropertyChanged
    {
        public SolutionInfo(IStatusInfoCollection ownerCollection, RepositoryInfo parentRepository, Solution solution, List<ProjectInfo> projectList)
        {
            OwnerCollection = ownerCollection;
            ParentRepository = parentRepository;
            Source = solution;
            ProjectList = projectList;
            IsValid = true;

            UpdateProjectDependencies();
        }

        private void UpdateProjectDependencies()
        {
            foreach (ProjectInfo Item in ProjectList)
                UpdateProjectDependencies(Item);
        }

        private void UpdateProjectDependencies(ProjectInfo project)
        {
            foreach (string ProjectGuid in project.Source.Dependencies)
                foreach (ProjectInfo Item in ProjectList)
                    if (Item.ProjectGuid == ProjectGuid)
                        project.Dependencies.Add(Item);
        }

        public IStatusInfoCollection OwnerCollection { get; }
        public RepositoryInfo ParentRepository { get; }
        public Solution Source { get; }
        public List<ProjectInfo> ProjectList { get; }
        public bool IsValid { get; private set; }
        public string Name { get { return Source.Name; } }

        public void Invalidate()
        {
            IsValid = false;
            ParentRepository.Invalidate();
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
