namespace Monitor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class ProjectValidation
    {
        public ProjectValidation(GitProbe gitProbe)
        {
            GitProbe = gitProbe;
        }

        public GitProbe GitProbe { get; }
        public List<MandatoryFile> MandatoryFileList { get; } = new();
        public List<MandatoryFile> MissingFileList { get; } = new();
        public List<MandatoryFile> InvalidFileList { get; } = new();

        public void AddMandatorySolutionFile(string fileName, byte[] content)
        {
            MandatorySolutionFile NewMandatoryFile = new(fileName, content);
            MandatoryFileList.Add(NewMandatoryFile);
        }

        public void AddMandatoryProjectFile(string projectName, string fileName, byte[] content)
        {
            MandatoryProjectFile NewMandatoryFile = new(projectName, fileName, content);
            MandatoryFileList.Add(NewMandatoryFile);
        }

        public async Task Validate()
        {
            foreach (RepositoryInfo Item in GitProbe.RepositoryList)
                await ValidateRepository(Item);
        }

        public async Task ValidateRepository(RepositoryInfo repository)
        {
            foreach (MandatoryFile Item in MandatoryFileList)
                await ValidateMandatoryFile(repository, Item);
        }

        public async Task ValidateMandatoryFile(RepositoryInfo repository, MandatoryFile mandatoryFile)
        {
            Dictionary<string, Stream?> DownloadResultTable = await GitProbe.DownloadRepositoryFile(repository, mandatoryFile.RootPath, mandatoryFile.FileName);
            byte[]? Content = null;

            foreach (KeyValuePair<string, Stream?> Entry in DownloadResultTable)
            {
                Stream? DownloadStream = Entry.Value;
                if (DownloadStream != null)
                {
                    using BinaryReader Reader = new(DownloadStream);
                    Content = Reader.ReadBytes((int)DownloadStream.Length);
                }

                break;
            }

            if (Content == null)
            {
                repository.Invalidate();
                MissingFileList.Add(mandatoryFile);
            }
            else if (!IsContentEqual(Content, mandatoryFile.Content))
            {
                repository.Invalidate();
                InvalidFileList.Add(mandatoryFile);
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
    }
}
