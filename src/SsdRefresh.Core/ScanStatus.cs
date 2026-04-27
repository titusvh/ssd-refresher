namespace SsdRefresh.Core;

public enum ScanStatus
{
    Hashed,
    SkippedUnauthorized,
    SkippedLocked,
    SkippedUnsupportedAttribute,
    SkippedReparsePoint,
    SkippedNotARegularFile,
    SkippedFileNotFound,
    FailedIo,
    FailedUnexpected,
}
