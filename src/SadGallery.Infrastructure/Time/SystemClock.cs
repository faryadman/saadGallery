using SadGallery.Application.Abstractions;

namespace SadGallery.Infrastructure.Time;

/// <inheritdoc />
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
