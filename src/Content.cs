namespace PhigrosJiu;

/// <summary>Central place for the resource paths the mod ships inside its pck.</summary>
public static class Content
{
    public const string Root = "res://PhigrosJiu/";

    public static string CardArt(string file) => Root + "cards/" + file;

    public static string PowerIcon(string entry) => Root + "icons/powers/" + ModelIdentity.AssetEntry(entry) + ".png";

    public static string RelicIcon(string entry) => Root + "icons/relics/" + ModelIdentity.AssetEntry(entry) + ".png";

    public static string CharacterIcon => Root + "character/icon.png";

    public const string CharacterSelectBg = Root + "character/char_select_bg.tscn";
    public const string CharacterVisuals = Root + "character/geopelia_visuals.tscn";
}
