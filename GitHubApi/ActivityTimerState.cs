namespace GitHubApi
{
    /// <summary>
    /// States of the activity polling.
    /// </summary>
    internal enum ActivityTimerState
    {
        /// <summary>
        /// Initialization.
        /// </summary>
        Init,

        /// <summary>
        /// Check for remaining requests.
        /// </summary>
        GetRemainingRequests,

        /// <summary>
        /// Reconnect to the server.
        /// </summary>
        Reconnect,

        /// <summary>
        /// Enumerate repositories with activity.
        /// </summary>
        EnumerateRepositories,
    }
}
