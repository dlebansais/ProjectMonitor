namespace GitHubApi
{
    using Octokit;

    /// <summary>
    /// A repository commit.
    /// </summary>
    public class GitHubRepository
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubRepository"/> class.
        /// </summary>
        /// <param name="source">The Octokit repository.</param>
        internal GitHubRepository(Repository source)
        {
            Source = source;
        }

        /// <summary>
        /// Gets the Octokit repository.
        /// </summary>
        internal Repository Source { get; }

        /// <summary>
        /// Gets the repository owner login name.
        /// </summary>
        public string Owner { get { return Source.Owner.Login; } }

        /// <summary>
        /// Gets the repository name.
        /// </summary>
        public string Name { get { return Source.Name; } }

        /// <summary>
        /// Gets the repository ID.
        /// </summary>
        public long Id { get { return Source.Id; } }

        /// <summary>
        /// Gets a value indicating whether the repository is private.
        /// </summary>
        public bool IsPrivate { get { return Source.Private; } }

        /// <summary>
        /// Gets the last commit on the master branch for this repository.
        /// </summary>
        public GitHubCommit? MasterCommit { get; private set; }

        /// <summary>
        /// Updates the last commit on the master branch for this repository.
        /// </summary>
        /// <param name="masterCommit">The last commit on the master branch.</param>
        internal void SetMasterCommit(GitHubCommit masterCommit)
        {
            MasterCommit = masterCommit;
        }
    }
}
