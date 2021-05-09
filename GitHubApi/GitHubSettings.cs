namespace GitHubApi
{
    public static class GitHubSettings
    {
        public static string OwnerName { get; set; } = string.Empty;
        public static string LoginName { get; set; } = string.Empty;
        public static string Token { get; set; } = string.Empty;
        internal static string ApplicationName { get; } = "Repo-Inspector";
    }
}
