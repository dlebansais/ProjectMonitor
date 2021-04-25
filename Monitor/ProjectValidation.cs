namespace Monitor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class ProjectValidation
    {
        public ProjectValidation(GitProbe gitProbe)
        {
            GitProbe = gitProbe;
        }

        public GitProbe GitProbe { get; }
        public List<MandatoryRepositoryFile> MandatoryRepositoryFileList { get; } = new();
        public List<MandatoryProjectFile> MandatoryProjectFileList { get; } = new();
        public List<MandatoryIgnoreLine> MandatoryIgnoreLineList { get; } = new();
        public List<MandatoryDependentProject> MandatoryDependentProjectList { get; } = new();
        public List<string> ErrorList { get; } = new();

        public void AddMandatoryRepositoryFile(string fileName, byte[] content)
        {
            MandatoryRepositoryFile NewMandatoryFile = new(fileName, content);
            MandatoryRepositoryFileList.Add(NewMandatoryFile);
        }

        public void AddMandatoryProjectFile(string projectName, string fileName, byte[] content)
        {
            MandatoryProjectFile NewMandatoryFile = new(projectName, fileName, content);
            MandatoryProjectFileList.Add(NewMandatoryFile);
        }

        public void AddMandatoryIgnoreLine(string line)
        {
            MandatoryIgnoreLine NewMandatoryIgnoreLine = new(line);
            MandatoryIgnoreLineList.Add(NewMandatoryIgnoreLine);
        }

        public void AddMandatoryDependentProject(string projectName)
        {
            MandatoryDependentProject NewMandatoryDependentProject = new(projectName);
            MandatoryDependentProjectList.Add(NewMandatoryDependentProject);
        }

        public async Task Validate()
        {
            foreach (RepositoryInfo Item in GitProbe.RepositoryList)
                await ValidateRepository(Item);

            foreach (SolutionInfo Item in GitProbe.SolutionList)
                await ValidateSolution(Item);
        }

        public async Task ValidateRepository(RepositoryInfo repository)
        {
            foreach (MandatoryFile Item in MandatoryRepositoryFileList)
            {
                bool IsValid = await ValidateMandatoryFile(repository, Item);
                if (!IsValid)
                    repository.Invalidate();
            }

            if (!repository.IsMainProjectExe)
                await CheckMandatoryIgnoreLine(repository);
        }

        public async Task CheckMandatoryIgnoreLine(RepositoryInfo repository)
        {
            byte[]? Content = null;

            Dictionary<string, Stream?> DownloadResultTable = await GitProbe.DownloadRepositoryFile(repository, "/", ".gitignore");
            if (DownloadResultTable.Count > 0)
            {
                KeyValuePair<string, Stream?> Entry = DownloadResultTable.First();
                Stream? DownloadStream = Entry.Value;

                if (DownloadStream != null)
                {
                    using BinaryReader Reader = new(DownloadStream);
                    Content = Reader.ReadBytes((int)DownloadStream.Length);
                }
            }

            if (Content == null)
            {
                repository.Invalidate();
                ErrorList.Add($"repo {repository.Name} is missing a .gitignore");
                return;
            }

            string StringContent = System.Text.Encoding.UTF8.GetString(Content);
            string[] Lines = StringContent.Split('\x0A');

            List<string> LineToCheckList = new();
            foreach (MandatoryIgnoreLine Item in MandatoryIgnoreLineList)
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
            foreach (MandatoryFile Item in MandatoryProjectFileList)
            {
                bool IsValid = await ValidateMandatoryFile(solution.Repository, Item);
                if (!IsValid)
                    solution.Invalidate();
            }

            foreach (MandatoryDependentProject Item in MandatoryDependentProjectList)
            {
                bool IsValid = ValidateMandatoryDependentProject(solution, Item);
                if (!IsValid)
                    solution.Invalidate();
            }
        }

        public async Task<bool> ValidateMandatoryFile(RepositoryInfo repository, MandatoryFile mandatoryFile)
        {
            Dictionary<string, Stream?> DownloadResultTable = await GitProbe.DownloadRepositoryFile(repository, mandatoryFile.RootPath, mandatoryFile.FileName);
            byte[]? Content;

            KeyValuePair<string, Stream?> Entry = DownloadResultTable.First();
            Stream? DownloadStream = Entry.Value;

            if (DownloadStream != null)
            {
                using BinaryReader Reader = new(DownloadStream);
                Content = Reader.ReadBytes((int)DownloadStream.Length);
            }
            else
                Content = null;

            string ErrorText;

            if (Content == null)
                ErrorText = $"In repo {repository.Name}, file {mandatoryFile.FileName} is missing";
            else if (!IsContentEqual(Content, mandatoryFile.Content))
                ErrorText = $"In repo {repository.Name}, file {mandatoryFile.FileName} is has invalid content";
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

        public bool ValidateMandatoryDependentProject(SolutionInfo solution, MandatoryDependentProject mandatoryDependentProject)
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

            return false;
        }
    }
}
