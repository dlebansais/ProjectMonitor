namespace Monitor
{
    using System;
    using System.Collections.ObjectModel;

    public class RepositoryInfoCollection : ObservableCollection<RepositoryInfo>, IStatusInfoCollection
    {
        public string Name { get; } = "Repositories";

        public double ValidPercentage
        {
            get
            {
                int ValidCount = 0;

                foreach (IStatusInfo Item in this)
                    if (Item.IsValid)
                        ValidCount++;

                return Count > 0 ? (1.0 * ValidCount) / Count : 0;
            }
        }

        public event EventHandler? ValidCountChanged;

        public void NotifyValidCountChanged()
        {
            ValidCountChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
