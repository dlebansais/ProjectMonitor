namespace Monitor
{
    public class MandatorySolutionFile : MandatoryFile
    {
        public MandatorySolutionFile(string fileName, byte[] content)
            : base(fileName, content)
        {
        }

        public override string RootPath { get { return "/"; } }
    }
}
