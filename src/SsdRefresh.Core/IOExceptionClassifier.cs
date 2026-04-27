using System.Runtime.InteropServices;

namespace SsdRefresh.Core;

internal static class IOExceptionClassifier
{
    private const int SharingViolationHResult = unchecked((int)0x80070020);
    private const int LockViolationHResult = unchecked((int)0x80070021);

    public static ScanStatus Classify(IOException exception)
    {
        if (exception.HResult is SharingViolationHResult or LockViolationHResult)
        {
            return ScanStatus.SkippedLocked;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var message = exception.Message.ToLowerInvariant();
            if (message.Contains("being used by another process", StringComparison.Ordinal) ||
                message.Contains("cannot access the file", StringComparison.Ordinal))
            {
                return ScanStatus.SkippedLocked;
            }
        }

        return ScanStatus.FailedIo;
    }
}
