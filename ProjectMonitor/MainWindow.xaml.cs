namespace ProjectMonitor
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using Monitor;

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            IsBusy = true;
            GitProbe = new GitProbe(UserLoginName, RepositoryList, SolutionList, ProjectList);
            Validation = new(GitProbe, ErrorList);

            circleRepository.Source = RepositoryList;
            circleSolution.Source = SolutionList;
            circleProject.Source = ProjectList;

            InitValidation();

            Loaded += OnLoaded;
        }

        public string UserLoginName { get; } = "dlebansais";
        public bool IsBusy { get; private set; }
        public bool IsConnected { get { return GitProbe.IsConnected; } }
        public bool IsSlowingDown { get { return GitProbe.IsSlowingDown; } }
        public RepositoryInfoCollection RepositoryList { get; } = new();
        public SolutionInfoCollection SolutionList { get; } = new();
        public ProjectInfoCollection ProjectList { get; } = new();
        public double RemainingRequests { get { return GitProbe.RemainingRequests; } }
        public ObservableCollection<string> ErrorList { get; } = new();

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            GitProbe.StatusUpdated += OnStatusUpdated;
            GitProbe.SlowDownChanged += OnSlowDownChanged;

            await GitProbe.Start();
            if (IsConnected)
                await Validation.Validate();
        }

        private void OnStatusUpdated(object sender, EventArgs args)
        {
            if (IsBusy)
            {
                IsBusy = false;
                NotifyPropertyChanged(nameof(IsBusy));
            }

            NotifyPropertyChanged(nameof(IsConnected));
            NotifyPropertyChanged(nameof(RemainingRequests));
        }

        private void OnSlowDownChanged(object sender, EventArgs args)
        {
            NotifyPropertyChanged(nameof(IsSlowingDown));
        }

        private void InitValidation()
        {
            List<string> MandatorySolutionList = new()
            {
                "deploy.bat",
                "signfile.bat",
                "updatecommit.bat",
                "updateversion.bat",
                ".editorconfig",
            };

            foreach (string MandatoryFileName in MandatorySolutionList)
            {
                byte[] FileContent = LoadResourceFile(MandatoryFileName);
                Validation.AddMandatoryRepositoryFile(MandatoryFileName, FileContent);
            }

            byte[] NugetConfigContent = LoadResourceFile("Resources.nuget.config");
            Validation.AddMandatoryRepositoryFile("nuget.config", NugetConfigContent);

            Validation.AddMandatoryIgnoreLine("/nuget");
            Validation.AddMandatoryIgnoreLine("/nuget-debug");
            Validation.AddMandatoryDependentProject("PreBuild");
            Validation.AddForbiddenProjectFile("packages.config");
            Validation.AddForbiddenProjectFile("GlobalSuppressions.cs");

            byte[] AppVeyorContentExe = LoadResourceFile("Resources.exe.appveyor.yml");
            byte[] AppVeyorContentLibrary = LoadResourceFile("Resources.dll.appveyor.yml");
            Validation.AddMandatoryContinuousIntegration(AppVeyorContentExe, AppVeyorContentLibrary);
        }

        private byte[] LoadResourceFile(string fileName)
        {
            Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
            using Stream ResourceStream = CurrentAssembly.GetManifestResourceStream($"ProjectMonitor.{fileName}");
            using BinaryReader Reader = new BinaryReader(ResourceStream);
            return Reader.ReadBytes((int)ResourceStream.Length);
        }

        private GitProbe GitProbe;
        private ProjectValidation Validation;

        #region Implementation of INotifyPropertyChanged
        /// <summary>
        /// Implements the PropertyChanged event.
        /// </summary>
#nullable disable annotations
        public event PropertyChangedEventHandler PropertyChanged;
#nullable restore annotations

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
