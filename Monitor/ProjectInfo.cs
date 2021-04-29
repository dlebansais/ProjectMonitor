namespace Monitor
{
    using SlnExplorer;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;

    [DebuggerDisplay("{ProjectName}")]
    public class ProjectInfo : IStatusInfo, INotifyPropertyChanged
    {
        public ProjectInfo(IStatusInfoCollection ownerCollection, Project project)
        {
            OwnerCollection = ownerCollection;
            Source = project;
            IsValid = true;
        }

        public IStatusInfoCollection OwnerCollection { get; }
        public SolutionInfo ParentSolution { get; set; } = null!;
        public Project Source { get; }
        public bool IsValid { get; private set; }
        public string ProjectName { get { return Source.ProjectName; } }
        public string ProjectGuid { get { return Source.ProjectGuid; } }
        public string RelativePath { get { return Source.RelativePath; } }
        public ProjectType ProjectType { get { return Source.ProjectType; } }
        public List<ProjectInfo> Dependencies { get; } = new();
        public SdkType SdkType { get { return Source.SdkType; } }
        public string LanguageVersion { get { return Source.LanguageVersion; } }
        public bool IsNullable { get { return Source.IsNullable; } }
        public string NeutralLanguage { get { return Source.NeutralLanguage; } }
        public bool IsEditorConfigLinked { get { return Source.IsEditorConfigLinked; } }
        public bool IsTreatWarningsAsErrors { get { return Source.IsTreatWarningsAsErrors; } }
        public IReadOnlyList<PackageReference> PackageReferenceList { get { return Source.PackageReferenceList; } }
        public IReadOnlyList<string> ProjectReferences { get { return Source.ProjectReferences; } }

        public void Invalidate()
        {
            IsValid = false;
            ParentSolution.Invalidate();
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
