namespace Monitor
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    public class ProjectValidation
    {
        public ProjectValidation(GitProbe gitProbe, ICollection<string> errorList)
        {
            GitProbe = gitProbe;
            ErrorList = errorList;
        }

        public GitProbe GitProbe { get; }
        public ICollection<string> ErrorList { get; }
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
            foreach (RepositoryInfo Item in GitProbe.RepositoryList)
                await ValidateRepository(Item);

            foreach (SolutionInfo Item in GitProbe.SolutionList)
                await ValidateSolution(Item);

            ValidatePackageReferenceVersions();
            ValidatePackageReferenceConditions();
            TagValidRepositories();
        }

        public async Task ValidateRepository(RepositoryInfo repository)
        {
            if (repository.SolutionList.Count == 0)
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

        public async Task CheckMandatoryIgnoreLine(RepositoryInfo repository)
        {
            byte[]? Content = await GitProbe.DownloadRepositoryFile(repository, "/.gitignore");

            if (Content == null)
            {
                repository.Invalidate();
                ErrorList.Add($"repo {repository.Name} is missing a .gitignore");
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
                ErrorList.Add($"repo {repository.Name} is missing {LineToCheckList.Count} lines in .gitignore");
            }
        }

        public async Task ValidateSolution(SolutionInfo solution)
        {
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

        public async Task<bool> ValidateMandatoryFile(RepositoryInfo repository, string path, RepositoryFile mandatoryFile)
        {
            return await ValidateContent(repository, path, mandatoryFile.FileName, mandatoryFile.Content, isMandatory: true);
        }

        public async Task<bool> ValidateForbiddenFile(RepositoryInfo repository, string path, RepositoryFile mandatoryFile)
        {
            return await ValidateContent(repository, path, mandatoryFile.FileName, mandatoryFile.Content, isMandatory: false);
        }

        public async Task<bool> ValidateContent(RepositoryInfo repository, string rootPath, string fileName, byte[] mandatoryContent, bool isMandatory)
        {
            byte[]? Content = await GitProbe.DownloadRepositoryFile(repository, $"{rootPath}{fileName}");

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
                ErrorList.Add(ErrorText);
                return false;
            }
            else
                return true;
        }

        public bool ValidateMandatoryDependentProject(SolutionInfo solution, DependentProject mandatoryDependentProject)
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
                ErrorList.Add($"Solution {solution.Name} is missing project {MandatoryProjectName}");
                return false;
            }

            bool IsDependent = true;
            foreach (ProjectInfo Project in solution.ProjectList)
                if (Project.ProjectGuid != DependentProject.ProjectGuid)
                    if (!IsRecursivelyDependent(Project, DependentProject.ProjectGuid))
                    {
                        ErrorList.Add($"In solution {solution.Name} project {Project.ProjectName} should depend on {MandatoryProjectName}");
                        IsDependent = false;
                    }

            return IsDependent;
        }

        public async Task<bool> ValidateMandatoryContinuousIntegration(RepositoryInfo repository, ContinuousIntegration mandatoryContinuousIntegration)
        {
            string ErrorText;
            byte[]? Content = await GitProbe.DownloadRepositoryFile(repository, "/appveyor.yml");

            if (Content == null)
                ErrorText = $"In repo {repository.Name}, continuous integration file is missing";
            else if (!IsContentEqual(Content, mandatoryContinuousIntegration.ContentExe) && !IsContentEqual(Content, mandatoryContinuousIntegration.ContentLibrary))
                ErrorText = $"In repo {repository.Name}, continuous integration file has invalid content";
            else
                ErrorText = string.Empty;

            if (ErrorText.Length > 0)
            {
                ErrorList.Add(ErrorText);
                return false;
            }
            else
                return true;
        }

        public void ValidateProjectQuality(ProjectInfo project)
        {
            if (project.SdkType == SlnExplorer.SdkType.Unknown && project.ProjectType == SlnExplorer.ProjectType.KnownToBeMSBuildFormat)
                return;

            if (project.SdkType != SlnExplorer.SdkType.Sdk)
            {
                ErrorList.Add($"Project {project.ProjectName} has wrong SDK type");
                project.Invalidate();
            }

            if (project.ProjectName != "PreBuild")
            {
                if (project.LanguageVersion != "9.0")
                {
                    ErrorList.Add($"Project {project.ProjectName} use wrong language version {project.LanguageVersion}");
                    project.Invalidate();
                }

                if (project.Nullable == SlnExplorer.NullableAnnotation.None)
                {
                    ErrorList.Add($"Project {project.ProjectName} doesn't have nullable set");
                    project.Invalidate();
                }

                if (project.NeutralLanguage != "en-US")
                {
                    ErrorList.Add($"Project {project.ProjectName} use wrong neutral language {project.NeutralLanguage}");
                    project.Invalidate();
                }

                if (!project.IsEditorConfigLinked)
                {
                    ErrorList.Add($"Project {project.ProjectName} doesn't have .editorconfig linked");
                    project.Invalidate();
                }

                if (!project.IsTreatWarningsAsErrors)
                {
                    ErrorList.Add($"Project {project.ProjectName} doesn't treat warnings as error");
                    project.Invalidate();
                }
            }
        }

        public void ValidatePackageReferenceVersions()
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

                    ErrorList.Add($"Package {Name} use referenced with several versions from {MinVersion} to {MaxVersion}");
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
                    ErrorList.Add($"Project {project.ProjectName} has package {ShortName}-Debug but no release version");
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
                        ErrorList.Add($"Project {project.ProjectName} use package {Name} but with wrong condition {Condition}");
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
    }
}
