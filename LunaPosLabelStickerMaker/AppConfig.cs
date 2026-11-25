namespace LunaPosLabelStickerMaker;

public static class AppConfig
{
#if TRIAL_MODE
    public const bool IsTrial = true;
#else
    public const bool IsTrial = false;
#endif

    public const string CurrentVersion = "1.0.0";
    public const int TrialDurationMinutes = 30;
    public const string AppEditionName = IsTrial ? "LunaPOS Label Sticker Maker (30-Minute Trial Edition)" : "LunaPOS Label Sticker Maker";
    public const string TrialContactPhone = "070-2220111";
    public const string TrialContactEmail = "sales@lunapos.lk";

    // GitHub Auto-Update Configuration
    public const string GitHubRepoOwner = "saadromez";
    public const string GitHubRepoName = "lunaposlabelstickermaker";
    public const string UpdateManifestUrl = "https://raw.githubusercontent.com/saadromez/lunaposlabelstickermaker/main/version.json";
    public const string GitHubReleasesApiUrl = "https://api.github.com/repos/saadromez/lunaposlabelstickermaker/releases/latest";
    public const string GitHubReleasesPageUrl = "https://github.com/saadromez/lunaposlabelstickermaker/releases";
    public const string GitHubRepoUrl = "https://github.com/saadromez/lunaposlabelstickermaker";
}
