namespace GitHubApi
{
    using Octokit;

    public class GitHubCommit
    {
        public GitHubCommit(GitHubBranch branch, GitReference source)
        {
            Branch = branch;
            Source = source;
        }

        public GitHubBranch Branch { get; }
        public GitHubRepository Repository { get { return Branch.Repository; } }
        internal GitReference Source { get; }

        public string Sha { get { return Source.Sha; } }
    }
}
