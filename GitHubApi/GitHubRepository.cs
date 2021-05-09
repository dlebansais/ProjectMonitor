namespace GitHubApi
{
    using Octokit;

    public class GitHubRepository
    {
        public GitHubRepository(Repository source)
        {
            Source = source;
        }

        internal Repository Source { get; }

        public string Owner { get { return Source.Owner.Login; } }
        public string Name { get { return Source.Name; } }
        public long Id { get { return Source.Id; } }
        public bool IsPrivate { get { return Source.Private; } }
        public GitHubCommit? MasterCommit { get; private set; }

        internal void SetMasterCommit(GitHubCommit masterCommit)
        {
            MasterCommit = masterCommit;
        }
    }
}
