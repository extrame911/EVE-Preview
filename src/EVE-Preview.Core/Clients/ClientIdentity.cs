namespace EvePreview.Core;

public sealed record ClientIdentity(int ProcessId, nint MainWindowHandle, DateTime ProcessStartTimeUtc);
