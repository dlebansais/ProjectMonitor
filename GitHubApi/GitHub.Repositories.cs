namespace GitHubApi
{
    using Octokit;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public static partial class GitHub
    {
        public static async Task<List<GitHubRepository>> EnumerateRepositories()
        {
            List<GitHubRepository> Result = new();

            if (await Connect())
            {
                SearchRepositoriesRequest Request = new() { User = User.Login };
                SearchRepositoryResult RequestResult = await Client.Search.SearchRepo(Request);

                foreach (Repository Repository in RequestResult.Items)
                {
                    if (Repository.Archived)
                        continue;

                    Result.Add(new GitHubRepository(Repository));
                }
            }

            return Result;
        }
    }
}
