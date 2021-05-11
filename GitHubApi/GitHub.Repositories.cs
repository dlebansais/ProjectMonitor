namespace GitHubApi
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Octokit;

    /// <summary>
    /// A simple class to enumerate repositories with .NET Sdk projects.
    /// </summary>
    public static partial class GitHub
    {
        /// <summary>
        /// Enumerate repositories.
        /// </summary>
        /// <returns>The list of repositories.</returns>
        public static async Task<List<GitHubRepository>> EnumerateRepositories()
        {
            List<GitHubRepository> Result = new();

            if (await Connect())
            {
                LastRepositorySearchTime = DateTimeOffset.UtcNow;
                SearchRepositoriesRequest Request = new() { User = User.Login };
                SearchRepositoryResult RequestResult = await Client.Search.SearchRepo(Request);

                foreach (Repository Repository in RequestResult.Items)
                {
                    if (Repository.Archived)
                        continue;

                    Result.Add(new GitHubRepository(Repository));
                }
            }

            lock (RepositoryActivityList)
            {
                RepositoryActivityList.Clear();
                RepositoryActivityList.AddRange(Result);
            }

            return Result;
        }
    }
}
