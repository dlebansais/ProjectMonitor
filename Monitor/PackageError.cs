namespace Monitor
{
    public class PackageError : MonitorError
    {
        public PackageError(string errorText)
        {
            ErrorText = errorText;
        }

        public override string ErrorText { get; }
    }
}
