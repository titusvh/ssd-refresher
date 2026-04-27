namespace SsdRefresh.Core;

public static class IOExceptionClassifier
{
    private const int SharingViolation = unchecked((int)0x80070020);
    private const int LockViolation = unchecked((int)0x80070021);

    public static ScanStatus Classify(IOException exception)
    {
        return exception.HResult switch
        {
            SharingViolation or LockViolation => ScanStatus.SkippedLocked,
            _ => ScanStatus.FailedIo
        };
    }
}
