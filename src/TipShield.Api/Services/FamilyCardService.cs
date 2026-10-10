namespace TipShield.Api.Services;

public interface IFamilyCardService
{
    string GenerateCard(int score, string verdict, List<string> topRedFlags, string language);
}

public sealed class FamilyCardService : IFamilyCardService
{
    public string GenerateCard(int score, string verdict, List<string> topRedFlags, string language)
    {
        var flags = string.Join("\n", topRedFlags.Take(3).Select((f, i) => $"{i + 1}. {f}"));

        if (language == "hi")
        {
            var flagBlock = string.IsNullOrEmpty(flags) ? "" : $"खतरे के संकेत:\n{flags}\n\n";
            return $"""
⚠️ TipShield चेतावनी ⚠️

यह संदेश संदिग्ध लगता है!
जोखिम स्कोर: {score}/100 ({TranslateVerdict(verdict, "hi")})

{flagBlock}✅ क्या करें:
• कोई भुगतान न करें
• SEBI की वेबसाइट पर सलाहकार की जांच करें
• cybercrime.gov.in पर शिकायत करें
• हेल्पलाइन: 1930

⚠️ यह सिर्फ शैक्षिक जानकारी है, वित्तीय सलाह नहीं।
— TipShield
""";
        }

        var enFlagBlock = string.IsNullOrEmpty(flags) ? "" : $"Warning signs:\n{flags}\n\n";
        return $"""
⚠️ TipShield Warning ⚠️

This message looks suspicious!
Risk Score: {score}/100 ({TranslateVerdict(verdict, "en")})

{enFlagBlock}✅ What to do:
• Do NOT pay any money
• Verify the advisor on SEBI's official website
• Report at cybercrime.gov.in
• Helpline: 1930

⚠️ Educational risk signals only. Not financial advice.
— TipShield
""";
    }

    private static string TranslateVerdict(string verdict, string language)
    {
        if (language == "hi")
        {
            return verdict switch
            {
                "likely_scam" => "संभवतः धोखाधड़ी",
                "suspicious" => "संदिग्ध",
                _ => "कोई स्पष्ट खतरा नहीं"
            };
        }
        return verdict switch
        {
            "likely_scam" => "Likely Scam",
            "suspicious" => "Suspicious",
            _ => "No Obvious Red Flags"
        };
    }
}
