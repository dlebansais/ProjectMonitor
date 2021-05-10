namespace Monitor
{
    public class RepositoryError : MonitorError
    {
        public RepositoryError(RepositoryInfo repository, string errorText)
        {
            Repository = repository;
            ErrorText = errorText;
        }

        public RepositoryInfo Repository { get; }
        public override string ErrorText { get; }
    }
}
