extends SceneTree

# Packs res://Geopelia (plus the Godot import cache it refers to) into the mod pck.
# Every entry keeps its res:// path, so the game resolves res://PhigrosJiu/... after mounting.

var files: Array[String] = []

func collect(path: String):
	for name in DirAccess.get_files_at(path):
		if not name.ends_with(".uid"):
			files.append(path.path_join(name))
			if name.ends_with(".import"):
				var config = ConfigFile.new()
				if config.load(path.path_join(name)) == OK:
					for key in config.get_section_keys("remap"):
						var value = config.get_value("remap", key)
						if value is String and value.begins_with("res://.godot/imported/") and not files.has(value):
							files.append(value)
	for name in DirAccess.get_directories_at(path):
		collect(path.path_join(name))

func _initialize():
	var args = OS.get_cmdline_user_args()
	if args.is_empty():
		quit(2)
		return
	collect("res://PhigrosJiu")
	files.sort()
	var pack = PCKPacker.new()
	if pack.pck_start(args[0]) != OK:
		quit(3)
		return
	for path in files:
		if pack.add_file(path, path) != OK:
			push_error("Failed to pack " + path)
			quit(4)
			return
	var err = pack.flush()
	print("GEOPELIA_PACK_FILES=", files.size(), " RESULT=", err)
	quit(err)
