namespace PocketMC.Infrastructure.Configuration;

/// <summary>
/// Authoritative community, documentation, and external service links backed by AppConfig (pocketmc.yml).
/// </summary>
public static class AppUrls
{
    public static string DiscordInvite => AppConfig.LinkDiscord;
    public static string Website => AppConfig.LinkWebsite;
    public static string GitHub => AppConfig.LinkGitHub;
    public static string Feedback => AppConfig.LinkFeedback;
    public static string YouTube => AppConfig.LinkYouTube;
    public static string Reddit => AppConfig.LinkReddit;
    public static string Instagram => AppConfig.LinkInstagram;
    public static string Donation => AppConfig.LinkDonation;
    public static string Studio => AppConfig.LinkOrganization;
    public static string NexViceDiscord => AppConfig.LinkDonatorNexVice;
    public static string DivyGitHub => AppConfig.LinkContributorDivy;
    public static string SahajGitHub => AppConfig.LinkContributorSahaj;
    public static string JohndinglesinGitHub => AppConfig.LinkHelperJohndinglesin;
}
