namespace Monitor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public class ProjectValidation
    {
        public ProjectValidation(GitProbe gitProbe, ICollection<MonitorError> errorList)
        {
            GitProbe = gitProbe;
            ErrorList = errorList;

            GitHubApi.GitHub.ActivityReported += OnActivityReported;
        }

        public GitProbe GitProbe { get; }
        public ICollection<MonitorError> ErrorList { get; }
        public List<RepositoryFile> MandatoryRepositoryFileList { get; } = new();
        public List<RepositoryFile> MandatoryProjectFileList { get; } = new();
        public List<RepositoryFile> ForbiddenProjectFileList { get; } = new();
        public List<IgnoreLine> MandatoryIgnoreLineList { get; } = new();
        public List<DependentProject> MandatoryDependentProjectList { get; } = new();
        public List<ContinuousIntegration> MandatoryContinuousIntegrationList { get; } = new();

        public void AddMandatoryRepositoryFile(string fileName, byte[] content)
        {
            RepositoryFile NewMandatoryFile = new(fileName, content);
            MandatoryRepositoryFileList.Add(NewMandatoryFile);
        }

        public void AddMandatoryProjectFile(string fileName, byte[] content)
        {
            RepositoryFile NewMandatoryFile = new(fileName, content);
            MandatoryProjectFileList.Add(NewMandatoryFile);
        }

        public void AddForbiddenProjectFile(string fileName)
        {
            RepositoryFile NewForbiddenFile = new(fileName, new byte[0]);
            ForbiddenProjectFileList.Add(NewForbiddenFile);
        }

        public void AddMandatoryIgnoreLine(string line)
        {
            IgnoreLine NewMandatoryIgnoreLine = new(line);
            MandatoryIgnoreLineList.Add(NewMandatoryIgnoreLine);
        }

        public void AddMandatoryDependentProject(string projectName)
        {
            DependentProject NewMandatoryDependentProject = new(projectName);
            MandatoryDependentProjectList.Add(NewMandatoryDependentProject);
        }

        public void AddMandatoryContinuousIntegration(byte[] contentExe, byte[] contentLibrary)
        {
            ContinuousIntegration NewMandatoryContinuousIntegration = new(contentExe, contentLibrary);
            MandatoryContinuousIntegrationList.Add(NewMandatoryContinuousIntegration);
        }

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
                ValidatePackageReferenceVersions();
                ValidatePackageReferenceConditions();
            }

            TagValidRepositories();

            GitHubApi.GitHub.SubscribeToActivity();
        }

        private async Task ValidateRepository(RepositoryInfo repository)
        {
            if (repository.SolutionList.Count == 0)
                return;

            if (GitProbe.IsKnownAsValid(repository))
                return;

            foreach (RepositoryFile Item in MandatoryRepositoryFileList)
            {
                bool IsValid = await ValidateMandatoryFile(repository, "/", Item);
                if (!IsValid)
                    repository.Invalidate();
            }

            if (!repository.IsMainProjectExe)
                await CheckMandatoryIgnoreLine(repository);

            foreach (ContinuousIntegration Item in MandatoryContinuousIntegrationList)
            {
                bool IsValid = await ValidateMandatoryContinuousIntegration(repository, Item);
                if (!IsValid)
                    repository.Invalidate();
            }
        }

        private async Task CheckMandatoryIgnoreLine(RepositoryInfo repository)
        {
            byte[]? Content = await GitHubApi.GitHub.DownloadFile(repository.Source, "/.gitignore");

            if (Content == null)
            {
                repository.Invalidate();

                string ErrorText = $"repo {repository.Name} is missing a .gitignore";
                ErrorList.Add(new RepositoryError(repository, ErrorText));
                return;
            }

            string StringContent = System.Text.Encoding.UTF8.GetString(Content);
            string[] Lines = StringContent.Split('\x0A');

            List<string> LineToCheckList = new();
            foreach (IgnoreLine Item in MandatoryIgnoreLineList)
                LineToCheckList.Add(Item.Line);

            foreach (string Item in Lines)
            {
                string Line = Item.EndsWith("\x0D") ? Item.Substring(0, Item.Length - 1) : Item;

                foreach (string LineToCheck in LineToCheckList)
                    if (Line == LineToCheck)
                    {
                        LineToCheckList.Remove(LineToCheck);
                        break;
                    }
            }

            if (LineToCheckList.Count > 0)
            {
                repository.Invalidate();

                string ErrorText = $"repo {repository.Name} is missing {LineToCheckList.Count} lines in .gitignore";
                ErrorList.Add(new RepositoryError(repository, ErrorText));
            }
        }

        private async Task ValidateSolution(SolutionInfo solution)
        {
            if (GitProbe.IsKnownAsValid(solution.ParentRepository))
                return;

            foreach (DependentProject Item in MandatoryDependentProjectList)
            {
                bool IsValid = ValidateMandatoryDependentProject(solution, Item);
                if (!IsValid)
                    solution.Invalidate();
            }

            foreach (ProjectInfo Project in solution.ProjectList)
            {
                string ProjectPath = Path.GetDirectoryName(Project.RelativePath);
                ProjectPath = $"/{ProjectPath}/";

                foreach (RepositoryFile Item in MandatoryProjectFileList)
                {
                    bool IsValid = await ValidateMandatoryFile(solution.ParentRepository, ProjectPath, Item);
                    if (!IsValid)
                        solution.Invalidate();
                }

                foreach (RepositoryFile Item in ForbiddenProjectFileList)
                {
                    bool IsValid = await ValidateForbiddenFile(solution.ParentRepository, ProjectPath, Item);
                    if (!IsValid)
                        solution.Invalidate();
                }

                ValidateProjectQuality(Project);
            }
        }

        private async Task<bool> ValidateMandatoryFile(RepositoryInfo repository, string path, RepositoryFile mandatoryFile)
        {
            return await ValidateContent(repository, path, mandatoryFile.FileName, mandatoryFile.Content, isMandatory: true);
        }

        private async Task<bool> ValidateForbiddenFile(RepositoryInfo repository, string path, RepositoryFile mandatoryFile)
        {
            return await ValidateContent(repository, path, mandatoryFile.FileName, mandatoryFile.Content, isMandatory: false);
        }

        private async Task<bool> ValidateContent(RepositoryInfo repository, string rootPath, string fileName, byte[] mandatoryContent, bool isMandatory)
        {
            byte[]? Content = await GitHubApi.GitHub.DownloadFile(repository.Source, $"{rootPath}{fileName}");

            string ErrorText;

            if (isMandatory)
            {
                if (Content == null)
                    ErrorText = $"In repo {repository.Name}, file {fileName} is missing";
                else if (!IsContentEqual(Content, mandatoryContent))
                    ErrorText = $"In repo {repository.Name}, file {fileName} is has invalid content";
                else
                    ErrorText = string.Empty;
            }
            else if (Content != null)
                ErrorText = $"In repo {repository.Name}, file {fileName} is present";
            else
                ErrorText = string.Empty;

            if (ErrorText.Length > 0)
            {
                ErrorList.Add(new RepositoryError(repository, ErrorText));
                return false;
            }
            else
                return true;
        }

        private bool ValidateMandatoryDependentProject(SolutionInfo solution, DependentProject mandatoryDependentProject)
        {
            string MandatoryProjectName = mandatoryDependentProject.ProjectName;

            ProjectInfo? DependentProject = null;
            foreach (ProjectInfo Project in solution.ProjectList)
                if (Project.ProjectName == MandatoryProjectName)
                {
                    DependentProject = Project;
                    break;
                }

            if (DependentProject == null)
            {
                string ErrorText = $"Solution {solution.Name} is missing project {MandatoryProjectName}";
                ErrorList.Add(new RepositoryError(solution.ParentRepository, ErrorText));
                return false;
            }

            bool IsDependent = true;
            foreach (ProjectInfo Project in solution.ProjectList)
                if (Project.ProjectGuid != DependentProject.ProjectGuid)
                    if (!IsRecursivelyDependent(Project, DependentProject.ProjectGuid))
                    {
                        string ErrorText = $"In solution {solution.Name} project {Project.ProjectName} should depend on {MandatoryProjectName}";
                        ErrorList.Add(new RepositoryError(solution.ParentRepository, ErrorText));
                        IsDependent = false;
                    }

            return IsDependent;
        }

        private async Task<bool> ValidateMandatoryContinuousIntegration(RepositoryInfo repository, ContinuousIntegration mandatoryContinuousIntegration)
        {
            string ErrorText;
            byte[]? Content = await GitHubApi.GitHub.DownloadFile(repository.Source, "/appveyor.yml");

            if (Content == null)
                ErrorText = $"In repo {repository.Name}, continuous integration file is missing";
            else if (!IsContentEqual(Content, mandatoryContinuousIntegration.ContentExe) && !IsContentEqual(Content, mandatoryContinuousIntegration.ContentLibrary))
                ErrorText = $"In repo {repository.Name}, continuous integration file has invalid content";
            else
                ErrorText = string.Empty;

            if (ErrorText.Length > 0)
            {
                ErrorList.Add(new RepositoryError(repository, ErrorText));
                return false;
            }
            else
                return true;
        }

        private void ValidateProjectQuality(ProjectInfo project)
        {
            if (project.SdkType == SlnExplorer.SdkType.Unknown && project.ProjectType == SlnExplorer.ProjectType.KnownToBeMSBuildFormat)
                return;

            if (project.SdkType != SlnExplorer.SdkType.Sdk)
            {
                string ErrorText = $"Project {project.ProjectName} has wrong SDK type";
                ErrorList.Add(new RepositoryError(project.ParentSolution.ParentRepository, ErrorText));
                project.Invalidate();
            }

            if (project.ProjectName != "PreBuild")
            {
                if (project.LanguageVersion != "9.0")
                {
                    string ErrorText = $"Project {project.ProjectName} use wrong language version {project.LanguageVersion}";
                    ErrorList.Add(new RepositoryError(project.ParentSolution.ParentRepository, ErrorText));
                    project.Invalidate();
                }

                if (project.Nullable == SlnExplorer.NullableAnnotation.None)
                {
                    string ErrorText = $"Project {project.ProjectName} doesn't have nullable set";
                    ErrorList.Add(new RepositoryError(project.ParentSolution.ParentRepository, ErrorText));
                    project.Invalidate();
                }

                if (project.NeutralLanguage != "en-US")
                {
                    string ErrorText = $"Project {project.ProjectName} use wrong neutral language {project.NeutralLanguage}";
                    ErrorList.Add(new RepositoryError(project.ParentSolution.ParentRepository, ErrorText));
                    project.Invalidate();
                }

                if (!project.IsEditorConfigLinked)
                {
                    string ErrorText = $"Project {project.ProjectName} doesn't have .editorconfig linked";
                    ErrorList.Add(new RepositoryError(project.ParentSolution.ParentRepository, ErrorText));
                    project.Invalidate();
                }

                if (!project.IsTreatWarningsAsErrors)
                {
                    string ErrorText = $"Project {project.ProjectName} doesn't treat warnings as error";
                    ErrorList.Add(new RepositoryError(project.ParentSolution.ParentRepository, ErrorText));
                    project.Invalidate();
                }
            }
        }

        private void ValidatePackageReferenceVersions()
        {
            Dictionary<string, List<string>> PackageReferenceTable = new();

            foreach (ProjectInfo Project in GitProbe.ProjectList)
            {
                foreach (SlnExplorer.PackageReference PackageReference in Project.PackageReferenceList)
                {
                    string Name = PackageReference.Name;
                    string Version = PackageReference.Version;

                    if (!PackageReferenceTable.ContainsKey(Name))
                        PackageReferenceTable.Add(Name, new List<string>());

                    List<string> ReferenceList = PackageReferenceTable[Name];
                    if (!ReferenceList.Contains(Version))
                        ReferenceList.Add(Version);
                }
            }

            foreach (KeyValuePair<string, List<string>> Entry in PackageReferenceTable)
            {
                List<string> ReferenceList = Entry.Value;
                ReferenceList.Sort();
            }

            foreach (KeyValuePair<string, List<string>> Entry in PackageReferenceTable)
            {
                string Name = Entry.Key;
                List<string> ReferenceList = Entry.Value;

                if (ReferenceList.Count > 1)
                {
                    string MinVersion = ReferenceList[0];
                    string MaxVersion = ReferenceList[ReferenceList.Count - 1];

                    string ErrorText = $"Package {Name} referenced with several versions from {MinVersion} to {MaxVersion}";
                    ErrorList.Add(new PackageError(ErrorText));
                    InvalidateProjectsWithOldVersion(Name, MaxVersion);
                }
            }
        }

        private void InvalidateProjectsWithOldVersion(string name, string maxVersion)
        {
            foreach (ProjectInfo Project in GitProbe.ProjectList)
                InvalidateProjectsWithOldVersion(Project, name, maxVersion);
        }

        private void InvalidateProjectsWithOldVersion(ProjectInfo project, string name, string maxVersion)
        {
            foreach (SlnExplorer.PackageReference PackageReference in project.PackageReferenceList)
                if (PackageReference.Name == name && PackageReference.Version != maxVersion)
                {
                    project.Invalidate();
                    break;
                }
        }

        public void ValidatePackageReferenceConditions()
        {
            foreach (ProjectInfo Project in GitProbe.ProjectList)
                ValidatePackageReferenceConditions(Project);
        }

        public void ValidatePackageReferenceConditions(ProjectInfo project)
        {
            List<string> ShortNameList = new();

            foreach (SlnExplorer.PackageReference PackageReference in project.PackageReferenceList)
            {
                string Name = PackageReference.Name;
                if (!Name.EndsWith("-Debug"))
                    continue;

                string ShortName = Name.Substring(0, Name.Length - 6);
                ShortNameList.Add(ShortName);
            }

            List<string> ValidPackageList = new();

            foreach (string ShortName in ShortNameList)
            {
                bool HasMainPackage = false;
                foreach (SlnExplorer.PackageReference PackageReference in project.PackageReferenceList)
                    if (PackageReference.Name == ShortName)
                    {
                        HasMainPackage = true;
                        break;
                    }

                if (HasMainPackage)
                    ValidPackageList.Add(ShortName);
                else
                {
                    string ErrorText = $"Project {project.ProjectName} has package {ShortName}-Debug but no release version";
                    AddErrorIfNewOnly(project.ParentSolution.ParentRepository, ErrorText);
                    project.Invalidate();
                }
            }

            foreach (string ShortName in ValidPackageList)
            {
                string ShortNameDebug = $"{ShortName}-Debug";

                foreach (SlnExplorer.PackageReference PackageReference in project.PackageReferenceList)
                {
                    string Name = PackageReference.Name;
                    string Condition = PackageReference.Condition;

                    if ((Name == ShortName && Condition != "'$(Configuration)|$(Platform)'!='Debug|x64'") || (Name == ShortNameDebug && Condition != "'$(Configuration)|$(Platform)'=='Debug|x64'"))
                    {
                        string ErrorText = $"Project {project.ProjectName} use package {Name} but with wrong condition {Condition}";
                        AddErrorIfNewOnly(project.ParentSolution.ParentRepository, ErrorText);
                        project.Invalidate();
                    }
                }
            }
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

        private static bool IsRecursivelyDependent(ProjectInfo project, string projectGuid)
        {
            foreach (ProjectInfo Item in project.Dependencies)
                if (Item.ProjectGuid == projectGuid || IsRecursivelyDependent(Item, projectGuid))
                    return true;

            foreach (string Item in project.ProjectReferences)
                foreach (ProjectInfo OtherProject in project.ParentSolution.ProjectList)
                    if (Item == OtherProject.ProjectName)
                        if (IsRecursivelyDependent(OtherProject, projectGuid))
                            return true;

            return false;
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
