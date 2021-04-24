namespace Monitor
{
    using SlnExplorer;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.IO;
    using System.Reflection;
    using System.Windows;
    using System.Runtime.CompilerServices;
    using System.Diagnostics.CodeAnalysis;

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            IsBusy = true;
            GitProbe = new GitProbe(UserLoginName, RepositoryList, SolutionList, ProjectTable);
            Validation = new(GitProbe);

            InitValidation();

            Loaded += OnLoaded;
        }

        public string UserLoginName { get; } = "dlebansais";
        public bool IsBusy { get; private set; }
        public bool IsConnected { get; private set; }
        public ObservableCollection<RepositoryInfo> RepositoryList { get; } = new();
        public ObservableCollection<Solution> SolutionList { get; } = new();
        public Dictionary<Solution, List<Project>> ProjectTable { get; } = new();

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            IsConnected = await GitProbe.Start();
            if (IsConnected)
            {
                NotifyPropertyChanged(nameof(IsConnected));
                await Validation.Validate();
            }

            IsBusy = false;
            NotifyPropertyChanged(nameof(IsBusy));
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
