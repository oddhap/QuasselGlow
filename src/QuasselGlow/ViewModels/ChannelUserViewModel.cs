using Quassel.Client.Domain;

namespace QuasselGlow.ViewModels;

public sealed class ChannelUserViewModel : ViewModelBase
{
    private const string KnownModePriority = "qaohv";

    public ChannelUserViewModel(QuasselChannelUser model)
    {
        Model = model;
    }

    public QuasselChannelUser Model { get; }

    public string Nick => Model.Nick;

    public string Modes => Model.Modes;

    public string Prefix => Modes.Length > 0 ? ModeToPrefix(Modes[0]) : string.Empty;

    public string ModeSummary =>
        string.IsNullOrWhiteSpace(Modes) || (Modes.Length == 1 && !string.IsNullOrWhiteSpace(Prefix))
            ? string.Empty
            : $"+{Modes}";

    public bool HasModeSummary => !string.IsNullOrWhiteSpace(ModeSummary);

    public static int Compare(ChannelUserViewModel? left, ChannelUserViewModel? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return 1;
        }

        if (right is null)
        {
            return -1;
        }

        var leftRank = GetPrimaryModeRank(left.Modes);
        var rightRank = GetPrimaryModeRank(right.Modes);
        if (leftRank != rightRank)
        {
            return leftRank.CompareTo(rightRank);
        }

        return string.Compare(left.Nick, right.Nick, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetPrimaryModeRank(string modes)
    {
        if (string.IsNullOrWhiteSpace(modes))
        {
            return int.MaxValue;
        }

        var rank = KnownModePriority.IndexOf(char.ToLowerInvariant(modes[0]));
        return rank >= 0 ? rank : KnownModePriority.Length;
    }

    private static string ModeToPrefix(char mode)
    {
        return char.ToLowerInvariant(mode) switch
        {
            'q' => "~",
            'a' => "&",
            'o' => "@",
            'h' => "%",
            'v' => "+",
            _ => string.Empty
        };
    }
}
