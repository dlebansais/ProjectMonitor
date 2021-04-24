namespace Monitor
{
    public abstract class MandatoryFile
    {
        public MandatoryFile(string fileName, byte[] content)
        {
            FileName = fileName;
            Content = content;
        }

        public abstract string RootPath { get; }
        public string FileName { get; }
        public byte[] Content { get; }
    }
}
