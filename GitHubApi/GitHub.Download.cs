namespace GitHubApi
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Octokit;

    /// <summary>
    /// A simple class to enumerate repositories with .NET Sdk projects.
    /// </summary>
    public static partial class GitHub
    {
        /// <summary>
        /// Downloads a file from a repository.
        /// Any subsequent attempt to download the same file just return content from the local cache.
        /// See <see cref="ClearCache"/>.
        /// </summary>
        /// <param name="repository">The repository.</param>
        /// <param name="filePath">The file path from the root of the repository.</param>
        /// <returns>The file content, null if an error occurred.</returns>
        public static async Task<byte[]?> DownloadFile(GitHubRepository repository, string filePath)
        {
            byte[]? Result = null;

            if (await Connect())
            {
                string Login = repository.Source.Owner.Login;
                string Name = repository.Source.Name;

                string UpdatedFilePath = filePath.Replace("\\", "/");
                string RepositoryAddress = $"{Login}/{Name}";

                Debug.WriteLine($"Downloading {RepositoryAddress} {UpdatedFilePath}");

                if (DownloadCache.ContainsKey(RepositoryAddress))
                {
                    Dictionary<string, byte[]> RepositoryCache = DownloadCache[RepositoryAddress];
                    if (RepositoryCache.ContainsKey(UpdatedFilePath))
                    {
                        Debug.WriteLine($"  (Already downloaded)");
                        return RepositoryCache[UpdatedFilePath];
                    }
                }

                try
                {
                    Result = await Client.Repository.Content.GetRawContent(Login, Name, UpdatedFilePath);
                }
                catch (Exception e) when (e is NotFoundException)
                {
                    Debug.WriteLine("(not found)");
                }

                if (Result != null)
                {
                    if (!DownloadCache.ContainsKey(RepositoryAddress))
                        DownloadCache.Add(RepositoryAddress, new Dictionary<string, byte[]>());

                    Dictionary<string, byte[]> RepositoryCache = DownloadCache[RepositoryAddress];
                    if (!RepositoryCache.ContainsKey(UpdatedFilePath))
                        RepositoryCache.Add(UpdatedFilePath, Result);
                }

                return Result;
            }

            return Result;
        }

        /// <summary>
        /// Clears the file cache.
        /// </summary>
        public static void ClearCache()
        {
            DownloadCache.Clear();
        }

        private static Dictionary<string, Dictionary<string, byte[]>> DownloadCache = new();
    }
}
