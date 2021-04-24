namespace ProjectMonitor
{
    using Monitor;
    using System;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Controls;

    public partial class StatusCircleControl : UserControl, INotifyPropertyChanged
    {
        public StatusCircleControl()
        {
            InitializeComponent();
            DataContext = this;

            RepositoryInfoCollection EmptySource = new RepositoryInfoCollection();
            EmptySource.CollectionChanged += OnCollectionChanged;
            EmptySource.ValidCountChanged += OnValidCountChanged;

            SourceInternal = EmptySource;
        }

        public IStatusInfoCollection Source 
        { 
            get { return SourceInternal; }
            set
            {
                if (SourceInternal != value)
                {
                    SourceInternal.CollectionChanged -= OnCollectionChanged;
                    SourceInternal.ValidCountChanged -= OnValidCountChanged;

                    SourceInternal = value;
                    SourceInternal.CollectionChanged += OnCollectionChanged;
                    SourceInternal.ValidCountChanged += OnValidCountChanged;
                }
            }
        }
        private IStatusInfoCollection SourceInternal;

        public double Percentage
        { 
            get { return Source.ValidPercentage; }
        }

        public string CollectionName { get { return Source.Name; } }
        public int Count { get { return Source.Count; } }
        public bool HasValid { get { return Count > 0 && Source.ValidPercentage > 0; } }
        public bool HasOnlyValid { get { return Count > 0 && Source.ValidPercentage >= 1.0; } }
        public bool HasInvalid { get { return Count > 0 && Source.ValidPercentage < 1.0; } }
        public bool HasOnlyInvalid { get { return Count > 0 && Source.ValidPercentage <= 0; } }
        public bool IsLarge { get; private set; }
        public bool IsNotLarge { get { return !IsLarge; } }
        public Point Point { get; private set; } = new Point(100, 0);

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            NotifyPropertyChanged(nameof(Count));
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(RecalculateArc));
        }

        private void OnValidCountChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(RecalculateArc));
        }

        private void RecalculateArc()
        {
            NotifyPropertyChanged(nameof(HasValid));
            NotifyPropertyChanged(nameof(HasOnlyValid));
            NotifyPropertyChanged(nameof(HasInvalid));
            NotifyPropertyChanged(nameof(HasOnlyInvalid));

            double ValidPercentage = Source.ValidPercentage;
            double Ratio = (ValidPercentage < 0) ? 0 : ((ValidPercentage > 1.0) ? 1.0 : ValidPercentage);

            double X = 100 * (1.0 + Math.Sin(Math.PI * 2 * Ratio));
            double Y = 100 * (1.0 - Math.Cos(Math.PI * 2 * Ratio));

            IsLarge = Ratio >= 0.5;
            NotifyPropertyChanged(nameof(IsLarge));
            NotifyPropertyChanged(nameof(IsNotLarge));

            Point = new Point(X, Y);
            NotifyPropertyChanged(nameof(Point));
        }

        #region Implementation of INotifyPropertyChanged
        /// <summary>
        /// Implements the PropertyChanged event.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        internal void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification = "Default parameter is mandatory with [CallerMemberName]")]
        internal void NotifyThisPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
