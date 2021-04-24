namespace Monitor
{
    using System;
    using System.Collections.Specialized;

    public interface IStatusInfoCollection
    {
        string Name { get; }
        int Count { get; }
        double ValidPercentage { get; }
        event NotifyCollectionChangedEventHandler CollectionChanged;
        event EventHandler ValidCountChanged;
        void NotifyValidCountChanged();
    }
}
