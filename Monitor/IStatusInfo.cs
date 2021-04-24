namespace Monitor
{
    public interface IStatusInfo
    {
        bool IsValid { get; }
        void Invalidate();
    }
}
