namespace GitHubApi
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Octokit;

    /// <summary>
    /// A simple class to enumerate repositories with .NET Sdk projects.
    /// </summary>
    public static partial class GitHub
    {
        /// <summary>
        /// Gets or sets the default time between polling for new activity.
        /// </summary>
        public static TimeSpan DefaultActivityPollingTime { get; set; } = TimeSpan.FromSeconds(20);

        /// <summary>
        /// Subscribe to activity polling.
        /// </summary>
        public static void SubscribeToActivity()
        {
            if (ActivityTimer == null)
                ActivityTimer = new Timer(new TimerCallback(ActivityTimerCallback));

            RepositoryActivityTask = null;
            ActivityTimer.Change(DefaultActivityPollingTime, DefaultActivityPollingTime);
        }

        /// <summary>
        /// Unsubscribe to activity polling.
        /// </summary>
        public static void UnsubscribeToActivity()
        {
            if (ActivityTimer != null)
            {
                ActivityTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                ActivityTimer.Dispose();
                ActivityTimer = null;
            }
        }

        private static void ActivityTimerCallback(object parameter)
        {
            switch (State)
            {
                default:
                case ActivityTimerState.Init:
                case ActivityTimerState.GetRemainingRequests:
                    GetRateLimitsTask = Client.Miscellaneous.GetRateLimits();
                    State = ActivityTimerState.Reconnect;
                    break;

                case ActivityTimerState.Reconnect:
                    if (GetRateLimitsTask == null || !GetRateLimitsTask.IsCompleted)
                        return;

                    RemainingRequests = TranslateRateLimits(GetRateLimitsTask.Result);
                    GetRateLimitsTask = null;

                    if (RemainingRequests >= RemainingRequestsThreshold)
                    {
                        ScheduleActivityCheck();
                        State = ActivityTimerState.EnumerateRepositories;
                    }
                    else
                        State = ActivityTimerState.GetRemainingRequests;
                    break;

                case ActivityTimerState.EnumerateRepositories:
                    if (RepositoryActivityTask == null || !RepositoryActivityTask.IsCompleted)
                        return;

                    SearchRepositoryResult SearchResult = RepositoryActivityTask.Result;
                    RepositoryActivityTask = null;

                    OnActivitySearchComplete(SearchResult);
                    State = ActivityTimerState.GetRemainingRequests;
                    break;
            }
        }

        private static void ScheduleActivityCheck()
        {
            Debug.Assert(RepositoryActivityTask == null);

            List<GitHubRepository> RepositoryList = new();

            lock (RepositoryActivityList)
            {
                RepositoryList.AddRange(RepositoryActivityList);
            }

            SearchRepositoriesRequest Request = new() { User = User.Login, Updated = DateRange.GreaterThan(LastRepositorySearchTime) };
            RepositoryActivityTask = Client.Search.SearchRepo(Request);
            LastRepositorySearchTime = DateTimeOffset.UtcNow;
        }

        private static void OnActivitySearchComplete(SearchRepositoryResult searchResult)
        {
            List<long> ModifiedRepositoryIdList = new();

            foreach (Repository Repository in searchResult.Items)
            {
                if (Repository.Archived)
                    continue;

                ModifiedRepositoryIdList.Add(Repository.Id);
            }

            NotifyActivityReported(ModifiedRepositoryIdList);
        }

        /// <summary>
        /// An event called with the polling result.
        /// See <see cref="SubscribeToActivity"/> to enable this event.
        /// </summary>
        public static event EventHandler<ActivityReportedEventArgs>? ActivityReported;

        private static void NotifyActivityReported(List<long> modifiedRepositoryList)
        {
            ActivityReported?.Invoke(null, new ActivityReportedEventArgs(modifiedRepositoryList));
        }

        private static Timer? ActivityTimer;
        private static ActivityTimerState State;
        private static DateTimeOffset LastRepositorySearchTime = DateTimeOffset.MinValue;
        private static List<GitHubRepository> RepositoryActivityList = new();
        private static Task<MiscellaneousRateLimit>? GetRateLimitsTask;
        private static Task<SearchRepositoryResult>? RepositoryActivityTask;
    }
}
