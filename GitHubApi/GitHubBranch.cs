namespace GitHubApi
{
    using Octokit;

    /// <summary>
    /// A repository branch.
    /// </summary>
    public class GitHubBranch
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubBranch"/> class.
        /// </summary>
        /// <param name="repository">The repository with this branch.</param>
        /// <param name="source">The Octokit branch.</param>
        internal GitHubBranch(GitHubRepository repository, Branch source)
        {
            Repository = repository;
            Source = source;
        }

        /// <summary>
        /// Gets the repository with this branch.
        /// </summary>
        public GitHubRepository Repository { get; }

        /// <summary>
        /// Gets the Octokit branch.
        /// </summary>
        internal Branch Source { get; }

        /// <summary>
        /// Gets the branch name.
        /// </summary>
        public string Name { get { return Source.Name; } }
    }
}
