namespace GitHubApi
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Octokit;

    public static partial class GitHub
    {
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

        public static void ClearCache()
        {
            DownloadCache.Clear();
        }

        private static Dictionary<string, Dictionary<string, byte[]>> DownloadCache = new();
    }
}
