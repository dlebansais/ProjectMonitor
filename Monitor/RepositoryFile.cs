namespace Monitor
{
    public class RepositoryFile
    {
        public RepositoryFile(string fileName, byte[] content)
        {
            FileName = fileName;
            Content = content;
        }

        public string FileName { get; }
        public byte[] Content { get; }
    }
}
