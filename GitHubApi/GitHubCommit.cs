namespace GitHubApi
{
    using Octokit;

    /// <summary>
    /// A repository commit.
    /// </summary>
    public class GitHubCommit
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubCommit"/> class.
        /// </summary>
        /// <param name="branch">The branch with this commit.</param>
        /// <param name="source">The Octokit commit.</param>
        internal GitHubCommit(GitHubBranch branch, GitReference source)
        {
            Branch = branch;
            Source = source;
        }

        /// <summary>
        /// Gets the branch with this commit.
        /// </summary>
        public GitHubBranch Branch { get; }

        /// <summary>
        /// Gets the repository with this commit.
        /// </summary>
        public GitHubRepository Repository { get { return Branch.Repository; } }

        /// <summary>
        /// Gets the Octokit commit.
        /// </summary>
        internal GitReference Source { get; }

        /// <summary>
        /// Gets the commit SHA.
        /// </summary>
        public string Sha { get { return Source.Sha; } }
    }
}
