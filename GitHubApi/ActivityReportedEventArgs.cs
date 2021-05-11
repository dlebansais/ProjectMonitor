namespace GitHubApi
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Arguments of the <see cref="GitHub.ActivityReported"/> event.
    /// </summary>
    public class ActivityReportedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityReportedEventArgs"/> class.
        /// </summary>
        /// <param name="modifiedRepositoryIdList">The list of repository IDs that had activity.</param>
        internal ActivityReportedEventArgs(List<long> modifiedRepositoryIdList)
        {
            ModifiedRepositoryIdList = modifiedRepositoryIdList;
        }

        /// <summary>
        /// Gets the list of repository IDs that had activity.
        /// </summary>
        public List<long> ModifiedRepositoryIdList { get; }
    }
}
