namespace Monitor
{
    public class ContinuousIntegration
    {
        public ContinuousIntegration(byte[] contentExe, byte[] contentLibrary)
        {
            ContentExe = contentExe;
            ContentLibrary = contentLibrary;
        }

        public byte[] ContentExe { get; }
        public byte[] ContentLibrary { get; }
    }
}
