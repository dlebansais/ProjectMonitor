namespace Monitor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public partial class ProjectValidation
    {
        public List<RepositoryFile> MandatoryProjectFileList { get; } = new();
        public List<RepositoryFile> ForbiddenProjectFileList { get; } = new();
        public List<DependentProject> MandatoryDependentProjectList { get; } = new();

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

        public void AddMandatoryDependentProject(string projectName)
        {
            DependentProject NewMandatoryDependentProject = new(projectName);
            MandatoryDependentProjectList.Add(NewMandatoryDependentProject);
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
    }
}
