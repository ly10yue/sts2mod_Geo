extends SceneTree

var failures: Array[String] = []
var checks := 0

func check(value: bool, label: String):
    checks += 1
    if not value:
        failures.append(label)

func parse(path: String):
    return JSON.parse_string(FileAccess.get_file_as_string(path))

func texture(path: String, width: int, height: int):
    var tex = load(path)
    check(tex is CompressedTexture2D, path)
    if tex is CompressedTexture2D:
        check(tex.get_width() == width and tex.get_height() == height, path + " dimensions")

func _initialize():
    var args = OS.get_cmdline_user_args()
    if args.size() != 2:
        quit(2)
        return
    check(ProjectSettings.load_resource_pack(args[0], false), "mount game")
    check(ProjectSettings.load_resource_pack(args[1], false), "mount mod")
    var cards = parse("res://PhigrosJiu/cards.json")
    var mechanics = parse("res://PhigrosJiu/mechanics.json")
    check(cards.size() == 89, "89 main-pool cards")
    var rarities = {}
    for card in cards:
        rarities[card.rarity] = rarities.get(card.rarity, 0) + 1
        var art = load("res://PhigrosJiu/cards/" + card.portrait)
        check(art is AtlasTexture, card.name + " exact song atlas")
        if art is AtlasTexture:
            check(art.atlas is CompressedTexture2D, card.name + " original image loaded")
            check(art.get_width() * 3 == art.get_height() * 4, card.name + " 4:3 view")
            check(art.atlas.resource_path == "res://PhigrosJiu/cards/originals/" + card.songId + ".png", card.name + " source matches song ID")
    for rarity in mechanics.rarities:
        check(int(rarities.get(rarity, 0)) == int(mechanics.rarities[rarity]), "rarity " + rarity)
    for power in mechanics.powers:
        texture("res://PhigrosJiu/icons/powers/" + power.icon, 128, 128)
    var relics = ["JUDGMENT_LINE", "SCORE_FRAGMENT", "STARDUST_BADGE", "PERFECT_FRAME"]
    for relic in relics:
        texture("res://PhigrosJiu/icons/relics/geopelia_" + relic.to_lower() + ".png", 128, 128)
    for lang in ["zhs", "eng"]:
        var ct = parse("res://PhigrosJiu/localization/" + lang + "/cards.json")
        var pt = parse("res://PhigrosJiu/localization/" + lang + "/powers.json")
        var rt = parse("res://PhigrosJiu/localization/" + lang + "/relics.json")
        for card in cards:
            check(ct.has(card.entry + ".title") and ct.has(card.entry + ".description"), lang + "/" + card.entry)
        for power in mechanics.powers:
            check(pt.has(power.entry + ".title") and pt.has(power.entry + ".description"), lang + "/" + power.entry)
        for relic in relics:
            check(rt.has("PHIGROS_JIU_GEOPELIA_" + relic + ".description"), lang + "/" + relic)
    var background = load("res://PhigrosJiu/character/char_select_bg.tscn")
    check(background is PackedScene, "character background")
    if background is PackedScene:
        var node = background.instantiate()
        check(node is Control, "background root")
        node.free()
    if failures.is_empty():
        print("JIU_DESIGN_RESOURCES=PASS checks=%d cards=89 powers=%d" % [checks, mechanics.powers.size()])
        quit(0)
    else:
        for failure in failures:
            printerr("FAIL: " + failure)
        quit(1)
