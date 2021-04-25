namespace Monitor
{
    public class MandatoryRepositoryFile : MandatoryFile
    {
        public MandatoryRepositoryFile(string fileName, byte[] content)
            : base(fileName, content)
        {
        }

        public override string RootPath { get { return "/"; } }
    }
}
