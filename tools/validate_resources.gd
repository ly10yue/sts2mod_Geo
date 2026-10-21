extends SceneTree

# Validates the packed mod against the real game data:
#   -- <game.pck> <mod.pck>
# Every asset the C# side references is loaded here, every localisation key the game will
# ask for is looked up, and every vanilla fallback path the character patches redirect to
# must exist in the game pck. Any failure exits non-zero so the build stops.

const CARD_IDS := [
	"GEOPELIA_STRIKE", "GEOPELIA_DEFEND", "GEOPELIA_OPENING_BEAT", "GEOPELIA_ELTAW",
	"GEOPELIA_I_MUST_SAY_NO", "GEOPELIA_CERERIS", "GEOPELIA_REIMEI", "GEOPELIA_REBIRTH",
	"GEOPELIA_LUMINESCENCE", "GEOPELIA_DISTORTED_FATE", "GEOPELIA_DANCE_WITH_SILENCE",
	"GEOPELIA_TERABYTE_CONNECTION", "GEOPELIA_DIAMOND_EYES", "GEOPELIA_RETRIBUTION",
	"GEOPELIA_BELIEVE_LIGHT", "GEOPELIA_ABSOLUTE_DISORDER", "GEOPELIA_FEAST",
	"GEOPELIA_CHRISTMAS_CAROL", "GEOPELIA_CRUSH_BETA", "GEOPELIA_HAPPY_DISEASE",
]

const POWER_IDS := [
	"GEOPELIA_COMBO_POWER", "GEOPELIA_BEAT_GUARD_POWER",
	"GEOPELIA_FEAST_POWER", "GEOPELIA_HAPPY_DISEASE_POWER",
]

const RELIC_IDS := [
	"GEOPELIA_JUDGMENT_LINE", "GEOPELIA_SCORE_FRAGMENT",
	"GEOPELIA_STARDUST_BADGE", "GEOPELIA_PERFECT_FRAME",
]

const CHARACTER_KEYS := [
	"title", "titleObject", "description", "pronounSubject", "pronounObject",
	"pronounPossessive", "possessiveAdjective", "bestiaryQuote", "cardsModifierTitle",
	"cardsModifierDescription", "eventDeathPrevention", "unlockText",
	"banter.alive.endTurnPing", "banter.dead.endTurnPing", "aromaPrinciple", "goldMonologue",
]

# Paths that GeopeliaCharacterPatches redirects the character to (vanilla Silent assets).
const VANILLA_FALLBACKS := [
	"res://scenes/creature_visuals/silent.tscn",
	"res://scenes/combat/energy_counters/silent_energy_counter.tscn",
	"res://scenes/merchant/characters/silent_merchant.tscn",
	"res://scenes/rest_site/characters/silent_rest_site.tscn",
	"res://scenes/ui/character_icons/silent_icon.tscn",
	"res://scenes/vfx/card_trail_silent.tscn",
	"res://images/ui/hands/multiplayer_hand_silent_point.png",
	"res://images/ui/hands/multiplayer_hand_silent_rock.png",
	"res://images/ui/hands/multiplayer_hand_silent_paper.png",
	"res://images/ui/hands/multiplayer_hand_silent_scissors.png",
	"res://images/packed/character_select/char_select_silent.png",
	"res://images/packed/map/icons/map_marker_silent.png",
	"res://materials/transitions/silent_transition_mat.tres",
]

var failures: Array[String] = []
var checks := 0

func check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures.append(message)

func read_json(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		return {}
	var parsed = JSON.parse_string(FileAccess.get_file_as_string(path))
	return parsed if parsed is Dictionary else {}

func expect_texture(path: String, width: int, height: int) -> void:
	var texture = load(path)
	check(texture is CompressedTexture2D, "missing texture: " + path)
	if texture is CompressedTexture2D:
		check(texture.get_width() == width and texture.get_height() == height,
			"wrong size for %s: %dx%d" % [path, texture.get_width(), texture.get_height()])

func _initialize():
	var args = OS.get_cmdline_user_args()
	if args.size() != 2:
		printerr("usage: validate_resources.gd -- <game.pck> <mod.pck>")
		quit(2)
		return
	check(ProjectSettings.load_resource_pack(args[0], false), "cannot mount game pck: " + args[0])
	check(ProjectSettings.load_resource_pack(args[1], false), "cannot mount mod pck: " + args[1])

	# --- art -------------------------------------------------------------
	for card_id in CARD_IDS:
		expect_texture("res://PhigrosJiu/cards/%s.png" % card_id.to_lower(), 640, 480)
	var icon = load("res://PhigrosJiu/character/icon.png")
	check(icon is CompressedTexture2D, "missing character icon")
	var selection = load("res://PhigrosJiu/character/selection.png")
	check(selection is CompressedTexture2D, "missing character selection art")

	for power_id in POWER_IDS:
		expect_texture("res://PhigrosJiu/icons/powers/%s.png" % power_id.to_lower(), 128, 128)
	for relic_id in RELIC_IDS:
		expect_texture("res://PhigrosJiu/icons/relics/%s.png" % relic_id.to_lower(), 128, 128)

	var background = load("res://PhigrosJiu/character/char_select_bg.tscn")
	check(background is PackedScene, "character select background is not a PackedScene")
	if background is PackedScene:
		var instance = background.instantiate()
		check(instance is Control, "character select background root is not a Control")
		instance.free()

	# --- localisation ----------------------------------------------------
	for language in ["eng", "zhs"]:
		var cards := read_json("res://PhigrosJiu/localization/%s/cards.json" % language)
		var characters := read_json("res://PhigrosJiu/localization/%s/characters.json" % language)
		var powers := read_json("res://PhigrosJiu/localization/%s/powers.json" % language)
		var relics := read_json("res://PhigrosJiu/localization/%s/relics.json" % language)
		check(not cards.is_empty(), "no cards table for " + language)
		check(not characters.is_empty(), "no characters table for " + language)
		check(not powers.is_empty(), "no powers table for " + language)
		check(not relics.is_empty(), "no relics table for " + language)
		for card_id in CARD_IDS:
			check(cards.has("PHIGROS_JIU_" + card_id + ".title"), "%s: missing %s.title" % [language, card_id])
			check(cards.has("PHIGROS_JIU_" + card_id + ".description"), "%s: missing %s.description" % [language, card_id])
		for power_id in POWER_IDS:
			check(powers.has("PHIGROS_JIU_" + power_id + ".title"), "%s: missing %s.title" % [language, power_id])
			check(powers.has("PHIGROS_JIU_" + power_id + ".description"), "%s: missing %s.description" % [language, power_id])
		for relic_id in RELIC_IDS:
			check(relics.has("PHIGROS_JIU_" + relic_id + ".title"), "%s: missing %s.title" % [language, relic_id])
			check(relics.has("PHIGROS_JIU_" + relic_id + ".description"), "%s: missing %s.description" % [language, relic_id])
		for key in CHARACTER_KEYS:
			check(characters.has("PHIGROS_JIU_GEOPELIA." + key), "%s: missing GEOPELIA.%s" % [language, key])

	# --- vanilla assets the character redirects to ------------------------
	for path in VANILLA_FALLBACKS:
		check(ResourceLoader.exists(path), "vanilla fallback missing: " + path)

	# --- result ----------------------------------------------------------
	if failures.is_empty():
		print("GEOPELIA_RESOURCE_VALIDATION=PASS checks=%d cards=%d powers=%d relics=%d" % [
			checks, CARD_IDS.size(), POWER_IDS.size(), RELIC_IDS.size()])
		quit(0)
		return
	for failure in failures:
		printerr("VALIDATION FAIL: " + failure)
	print("GEOPELIA_RESOURCE_VALIDATION=FAIL checks=%d failures=%d" % [checks, failures.size()])
	quit(1)
