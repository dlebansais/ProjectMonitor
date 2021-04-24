namespace Monitor
{
    public class MandatoryProjectFile : MandatoryFile
    {
        public MandatoryProjectFile(string projectName, string fileName, byte[] content)
            : base(fileName, content)
        {
            ProjectName = projectName;
        }

        public string ProjectName { get; }
        public override string RootPath { get { return $"/{ProjectName}"; } }
    }
}
