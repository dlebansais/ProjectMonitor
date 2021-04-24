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
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));
                await ValidateRepository(Item);
            }
        }

        public async Task ValidateRepository(RepositoryInfo repository)
        {
            foreach (MandatoryFile Item in MandatoryFileList)
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));
                await ValidateMandatoryFile(repository, Item);
            }
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
                MissingFileList.Add(mandatoryFile);
            else if (!IsContentEqual(Content, mandatoryFile.Content))
                InvalidFileList.Add(mandatoryFile);
        }

        private static bool IsContentEqual(byte[] content1, byte[] content2)
        {
            ReadOnlySpan<byte> Span1 = new(content1);
            ReadOnlySpan<byte> Span2 = new(content2);

            return Span1.SequenceEqual(Span2);
        }
    }
}
