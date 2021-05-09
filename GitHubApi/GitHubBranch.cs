namespace GitHubApi
{
    using Octokit;

    public class GitHubBranch
    {
        public GitHubBranch(GitHubRepository repository, Branch source)
        {
            Repository = repository;
            Source = source;
        }

        public GitHubRepository Repository { get; }
        internal Branch Source { get; }

        public string Name { get { return Source.Name; } }
        //public GitReference Commit { get { return Source.Commit; } }
    }
}
