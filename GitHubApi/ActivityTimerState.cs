namespace GitHubApi
{
    internal enum ActivityTimerState
    {
        Init,
        GetRemainingRequests,
        Reconnect,
        EnumerateRepositories,
    }
}
