namespace Monitor
{
    using System;
    using System.Collections.Generic;

    public class ActivityReportedArgs : EventArgs
    {
        public ActivityReportedArgs(List<MonitorError> errorToRemoveList)
        {
            ErrorToRemoveList = errorToRemoveList;
        }

        public List<MonitorError> ErrorToRemoveList { get; }
    }
}
