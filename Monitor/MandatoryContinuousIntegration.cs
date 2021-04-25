namespace Monitor
{
    public class MandatoryContinuousIntegration
    {
        public MandatoryContinuousIntegration(byte[] contentExe, byte[] contentLibrary)
        {
            ContentExe = contentExe;
            ContentLibrary = contentLibrary;
        }

        public byte[] ContentExe { get; }
        public byte[] ContentLibrary { get; }
    }
}
