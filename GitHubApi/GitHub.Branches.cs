namespace GitHubApi
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Octokit;

    /// <summary>
    /// A simple class to enumerate repositories with .NET Sdk projects.
    /// </summary>
    public static partial class GitHub
    {
        /// <summary>
        /// Enumerates branches of a repository.
        /// </summary>
        /// <param name="repository">The repository.</param>
        /// <returns>A list of branches.</returns>
        public static async Task<List<GitHubBranch>> EnumerateBranches(GitHubRepository repository)
        {
            List<GitHubBranch> Result = new();

            if (await Connect())
            {
                IReadOnlyList<Branch> BranchList = await Client.Repository.Branch.GetAll(repository.Source.Id);

                foreach (Branch Branch in BranchList)
                {
                    GitHubBranch NewBranch = new(repository, Branch);
                    Result.Add(NewBranch);

                    if (Branch.Name == "master")
                    {
                        repository.SetMasterCommit(new GitHubCommit(NewBranch, Branch.Commit));
                        break;
                    }
                }
            }

            return Result;
        }
    }
}
