namespace TipShield.Core.Models;

public enum Verdict
{
    NoObviousRedFlags,
    Suspicious,
    LikelyScam
}

public static class VerdictExtensions
{
    public static Verdict FromScore(int score) => score switch
    {
        >= 65 => Verdict.LikelyScam,
        >= 35 => Verdict.Suspicious,
        _ => Verdict.NoObviousRedFlags
    };

    public static string ToDisplayString(this Verdict verdict) => verdict switch
    {
        Verdict.LikelyScam => "likely_scam",
        Verdict.Suspicious => "suspicious",
        Verdict.NoObviousRedFlags => "no_obvious_red_flags",
        _ => "unknown"
    };
}

