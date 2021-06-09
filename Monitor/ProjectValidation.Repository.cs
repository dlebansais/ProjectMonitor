namespace Monitor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public partial class ProjectValidation
    {
        public List<RepositoryFile> MandatoryRepositoryFileList { get; } = new();
        public List<IgnoreLine> MandatoryIgnoreLineList { get; } = new();
        public List<ContinuousIntegration> MandatoryContinuousIntegrationList { get; } = new();

        public void AddMandatoryRepositoryFile(string fileName, byte[] content)
        {
            RepositoryFile NewMandatoryFile = new(fileName, content);
            MandatoryRepositoryFileList.Add(NewMandatoryFile);
        }

        public void AddMandatoryIgnoreLine(string line)
        {
            IgnoreLine NewMandatoryIgnoreLine = new(line);
            MandatoryIgnoreLineList.Add(NewMandatoryIgnoreLine);
        }

        public void AddMandatoryContinuousIntegration(byte[] contentExe, byte[] contentLibrary)
        {
            ContinuousIntegration NewMandatoryContinuousIntegration = new(contentExe, contentLibrary);
            MandatoryContinuousIntegrationList.Add(NewMandatoryContinuousIntegration);
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
    }
}
