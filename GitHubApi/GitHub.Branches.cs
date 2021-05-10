namespace GitHubApi
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Octokit;

    public static partial class GitHub
    {
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
