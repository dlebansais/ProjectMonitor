namespace GitHubApi
{
    using System;
    using System.Collections.Generic;

    public class ActivityReportedEventArgs : EventArgs
    {
        public ActivityReportedEventArgs(List<long> modifiedRepositoryIdList)
        {
            ModifiedRepositoryIdList = modifiedRepositoryIdList;
        }

        public List<long> ModifiedRepositoryIdList { get; }
    }
}
