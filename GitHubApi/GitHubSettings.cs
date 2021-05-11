namespace GitHubApi
{
    /// <summary>
    /// Contains settings to connect to GitHub.com.
    /// </summary>
    public static class GitHubSettings
    {
        /// <summary>
        /// Gets or sets the owner name.
        /// </summary>
        public static string OwnerName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the owner login name.
        /// </summary>
        public static string LoginName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the connection token.
        /// </summary>
        public static string Token { get; set; } = string.Empty;

        /// <summary>
        /// Gets the application name.
        /// </summary>
        internal static string ApplicationName { get; } = "Repo-Inspector";
    }
}
