namespace GitHubApi
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Octokit;

    /// <summary>
    /// A simple class to enumerate repositories with .NET Sdk projects.
    /// </summary>
    public static partial class GitHub
    {
        /// <summary>
        /// The threshold of acceptable remaining number of requests.
        /// </summary>
        public const double RemainingRequestsThreshold = 0.8;

        /// <summary>
        /// The time between requests when slowing down below the threshold.
        /// </summary>
        public static readonly TimeSpan SlowdownTime = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets a value indicating whether the connection with github.com is established.
        /// </summary>
        public static bool IsConnected { get; private set; }

        /// <summary>
        /// Gets the ratio of remaining requests.
        /// </summary>
        public static double RemainingRequests { get; private set; }

        /// <summary>
        /// Gets a value indicating whether requests are slowed down.
        /// </summary>
        public static bool IsSlowingDown { get; private set; }

        private static async Task<bool> Connect()
        {
            if (IsConnected)
            {
                RemainingRequests = await GetRemainingRequests();

                if (RemainingRequests >= RemainingRequestsThreshold)
                    return true;
                else
                {
                    Disconnect();
                    await SlowDown();
                }
            }

            await Reconnect();

            return IsConnected;
        }

        private static void Disconnect()
        {
            Debug.Assert(IsConnected);

            Client = null!;
            User = null!;
            IsConnected = false;
        }

        private static async Task Reconnect()
        {
            Debug.Assert(!IsConnected);

            Credentials AuthenticationToken = new Credentials(GitHubSettings.Token);
            Client = new GitHubClient(new ProductHeaderValue(GitHubSettings.ApplicationName));
            Client.Credentials = AuthenticationToken;

            try
            {
                User = await Client.User.Get(GitHubSettings.OwnerName);
                IsConnected = true;
            }
            catch (AuthorizationException exception)
            {
                Debug.WriteLine($"Failed to connect: {exception.Message}");
            }
            catch
            {
                throw;
            }
        }

        private static async Task<double> GetRemainingRequests()
        {
            try
            {
                MiscellaneousRateLimit RateLimits = await Client.Miscellaneous.GetRateLimits();
                return TranslateRateLimits(RateLimits);
            }
            catch (ApiException exception)
            {
                Debug.WriteLine($"GitHub: {exception.Message}");
                return 0;
            }
        }

        private static double TranslateRateLimits(MiscellaneousRateLimit rateLimits)
        {
            RateLimit CoreRateLimit = rateLimits.Resources.Core;
            RateLimit SearchRateLimit = rateLimits.Resources.Search;

            double CoreRatio = (1.0 * CoreRateLimit.Remaining) / CoreRateLimit.Limit;
            double SearchRatio = (1.0 * SearchRateLimit.Remaining) / SearchRateLimit.Limit;
            double MinRatio = Math.Min(CoreRatio, SearchRatio);

            return MinRatio;
        }

        private static async Task SlowDown()
        {
            IsSlowingDown = true;

            await Task.Delay(SlowdownTime);

            IsSlowingDown = false;
        }

        private static GitHubClient Client = null!;
        private static User User = null!;
    }
}
