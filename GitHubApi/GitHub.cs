namespace GitHubApi
{
    using Octokit;
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    public static partial class GitHub
    {
        public const double RemainingRequestsThreshold = 0.8;
        public static readonly TimeSpan SlowdownTime = TimeSpan.FromSeconds(5);

        public static bool IsConnected { get; private set; }
        public static double RemainingRequests { get; private set; }
        public static bool IsSlowingDown { get; private set; }

        public static async Task<bool> Connect()
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
                RateLimit CoreRateLimit = RateLimits.Resources.Core;
                RateLimit SearchRateLimit = RateLimits.Resources.Search;

                double CoreRatio = (1.0 * CoreRateLimit.Remaining) / CoreRateLimit.Limit;
                double SearchRatio = (1.0 * SearchRateLimit.Remaining) / SearchRateLimit.Limit;
                double RemainingRequests = Math.Min(CoreRatio, SearchRatio);

                return RemainingRequests;
            }
            catch (ApiException exception)
            {
                Debug.WriteLine($"GitHub: {exception.Message}");
                return 0;
            }
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
