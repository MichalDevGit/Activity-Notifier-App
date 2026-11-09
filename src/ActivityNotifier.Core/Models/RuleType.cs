namespace ActivityNotifier.Core.Models;

/// <summary>The kinds of reminders the app can send.</summary>
public enum RuleType
{
    /// <summary>Drink water (once per local clock hour while active).</summary>
    Hydration,

    /// <summary>Short break (1–3 min) after continuous work.</summary>
    MicroBreak,

    /// <summary>Long break (10–15 min) after longer continuous work.</summary>
    LongBreak,

    /// <summary>Finger and wrist exercises after heavy typing.</summary>
    Fingers,
}
