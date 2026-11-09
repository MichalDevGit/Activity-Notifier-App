using ActivityNotifier.Core.Models;
using ActivityNotifier.Core.Options;

namespace ActivityNotifier.Core.Rules;

/// <summary>
/// Drink water: due once per <em>local</em> clock hour, after at least
/// <see cref="RulesOptions.HydrationMinActiveMinutes"/> active minutes in that hour.
/// </summary>
/// <remarks>
/// No catch-up: hours you were away are skipped. Coming back mid-hour gives one reminder for the current hour.
/// </remarks>
public sealed class HydrationRule : IActivityRule
{
    private readonly int _minActiveMinutes;

    /// <summary>Creates the rule.</summary>
    /// <param name="options">Rule thresholds.</param>
    public HydrationRule(RulesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _minActiveMinutes = options.HydrationMinActiveMinutes;
    }

    /// <inheritdoc />
    public RuleType Type => RuleType.Hydration;

    /// <inheritdoc />
    public int Priority => 3;

    /// <inheritdoc />
    public bool IsDue(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        DateTimeOffset hourStartUtc = LocalHourStartUtc(context.NowUtc, context.LocalTimeZone);

        if (context.LastFiredUtc(RuleType.Hydration) is { } lastFiredUtc && lastFiredUtc >= hourStartUtc)
        {
            return false; // already reminded this clock hour
        }

        int activeMinutesThisHour = context.Snapshot.RecentMinutes
            .Count(m => m.IsActive && m.MinuteStartUtc >= hourStartUtc);

        return activeMinutesThisHour >= _minActiveMinutes;
    }

    // "Clock hour" is a local-time idea (14:00–15:00 on the user's clock), so convert to local,
    // cut to the start of the hour, and convert back to UTC to compare with the stored UTC times.
    private static DateTimeOffset LocalHourStartUtc(DateTimeOffset nowUtc, TimeZoneInfo zone)
    {
        DateTimeOffset local = TimeZoneInfo.ConvertTime(nowUtc, zone);
        var localHourStart = new DateTimeOffset(local.Year, local.Month, local.Day, local.Hour, 0, 0, local.Offset);
        return localHourStart.ToUniversalTime();
    }
}
