namespace Swipewalk.Core.Standards;

/// <summary>
/// How binding a standard's own instrument is. Recorded on every <see cref="Standard"/> so a report never
/// implies a state IT policy carries the same legal weight as an enacted statute. "Regulation" also covers a
/// properly promulgated administrative rule (for example one that went through public notice-and-comment,
/// such as a state's administrative code chapter) even where the jurisdiction's own vocabulary differs.
/// </summary>
public enum LegalTier
{
    /// <summary>Enacted by a legislature (an act, code section or similar).</summary>
    Statute,

    /// <summary>A regulation, administrative code chapter or similarly promulgated rule made under a statute's
    /// authority (for example a Statutory Order and Regulation, or a state's Administrative Code).</summary>
    Regulation,

    /// <summary>An executive-branch or agency IT policy, standard or guideline -- not itself enacted or
    /// promulgated as a regulation, even where a statute directs an office to issue one.</summary>
    OfficialPolicy,

    /// <summary>A voluntary technical standard (e.g. an ETSI/CEN/CENELEC standard such as EN 301 549) that
    /// gains legal effect only because a law or regulation references it -- not itself enacted or promulgated,
    /// and not a plain agency policy either.</summary>
    TechnicalStandard,
}

public static class LegalTierExtensions
{
    public static string Display(this LegalTier tier) => tier switch
    {
        LegalTier.Statute => "Statute",
        LegalTier.Regulation => "Regulation",
        LegalTier.TechnicalStandard => "Technical standard (referenced by law)",
        _ => "Official policy",
    };
}
