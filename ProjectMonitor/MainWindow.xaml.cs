namespace Monitor
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Windows;

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            IsBusy = true;
            GitProbe = new GitProbe(UserLoginName, RepositoryList, SolutionList, ProjectList);
            Validation = new(GitProbe);

            circleRepository.Source = RepositoryList;
            circleSolution.Source = SolutionList;
            circleProject.Source = ProjectList;

            InitValidation();

            Loaded += OnLoaded;
        }

        public string UserLoginName { get; } = "dlebansais";
        public bool IsBusy { get; private set; }
        public bool IsConnected { get { return GitProbe.IsConnected; } }
        public RepositoryInfoCollection RepositoryList { get; } = new();
        public SolutionInfoCollection SolutionList { get; } = new();
        public ProjectInfoCollection ProjectList { get; } = new();

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            /*if (!IsConnected)
            {
                IsConnected = true;
                IsBusy = false;
                NotifyPropertyChanged(nameof(IsBusy));
                return;
            }*/

            GitProbe.Connected += OnConnected;

            await GitProbe.Start();
            if (IsConnected)
                await Validation.Validate();
        }

        private void OnConnected(object sender, EventArgs args)
        {
            IsBusy = false;
            NotifyPropertyChanged(nameof(IsBusy));
            NotifyPropertyChanged(nameof(IsConnected));
        }

        private void InitValidation()
        {
            byte[] SignFileContent = LoadResourceFile(SignFileName);
            Validation.AddMandatorySolutionFile(SignFileName, SignFileContent);

            byte[] UpdateCommitContent = LoadResourceFile(UpdateCommitName);
            Validation.AddMandatorySolutionFile(UpdateCommitName, UpdateCommitContent);

            byte[] UpdateVersionContent = LoadResourceFile(UpdateVersionName);
            Validation.AddMandatorySolutionFile(UpdateVersionName, UpdateVersionContent);
        }

        private byte[] LoadResourceFile(string fileName)
        {
            Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
            using Stream ResourceStream = CurrentAssembly.GetManifestResourceStream($"ProjectMonitor.{fileName}");
            using BinaryReader Reader = new BinaryReader(ResourceStream);
            return Reader.ReadBytes((int)ResourceStream.Length);
        }

        private const string SignFileName = "signfile.bat";
        private const string UpdateCommitName = "updatecommit.bat";
        private const string UpdateVersionName = "updateversion.bat";

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
