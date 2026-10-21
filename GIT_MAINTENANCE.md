# Git维护与发布

远端：<https://github.com/ly10yue/sts2mod_Geo>，主分支：`main`。

当前本机的 `.modding/PhigrosJiu` 就是工作目录；编辑器保存到这里，不需要向另一个副本同步源码。保存前的备份仍保存在游戏目录的 `.modding-backups/PhigrosJiu-card-edits/`。

## 目录

| 路径 | 用途 |
| --- | --- |
| `src/` | C#角色、卡牌、能力和兼容逻辑 |
| `tools/card_design.py` | 卡牌设计唯一编辑源 |
| `tools/card_editor.py`、`tools/card_editor_v2.html` | 当前网页编辑器 |
| `content/`、`localization/` | 生成后的设计、对应清单和文本 |
| `assets/song_illustrations/` | 原曲绘，Git LFS保存 |
| `godot/` | 可复原的Godot项目与人物/图标模板 |
| `dist/PhigrosJiu/` | 可复制到游戏mods目录的当前构建 |
| `dist/manifest.json` | 安装包文件大小与SHA-256，以及设计快照SHA-256 |
| `tests/` | 编辑器、同载和原生验证支持 |
| `research/` | 构建所需曲目元数据与历史设计参照 |

`build/`、编译缓存、日志、网页会话令牌、本机PID、反编译游戏源码、游戏二进制和历史备份均不进入版本库。

## 日常修改

```powershell
git status --short
git diff -- tools/card_design.py
git add tools/card_design.py
git commit -m "调整指定卡牌"
git push origin main
```

提交只保存已经写入文件的修改；编辑器未保存草稿留在浏览器本地。使用前先保存需要纳入提交的草稿。

## 更新安装包

在游戏目录的 `.modding/PhigrosJiu` 中运行，按本机工具位置传入参数：

```powershell
python -m unittest discover -s tests -p test_card_editor.py -v
.\build.ps1 -Godot 'C:\tools\Godot_v4.5.1-stable_win64_console.exe' -DotNetHost 'C:\tools\dotnet9\dotnet.exe' -NoInstall
# 可选：有本机游戏与jiu_2时执行隔离原生验证。
.\validate-native.ps1 -Staged -EditorFeatures
python tools/package_mod.py
git status --short
git add src tools tests content localization godot dist *.md *.ps1 .gitignore .gitattributes
git commit -m "更新Mod与编辑器安装包"
git push origin main
```

`package_mod.py` 检查DLL/PCK存在且晚于源文件，然后更新`dist/`和校验清单，不替换正在使用的游戏Mod。

## 拉取与恢复

拉取别人提交前，先用`git status`检查自己的修改；提交后再`git pull --ff-only`，随后`git lfs pull`。

```powershell
git log --oneline -10
git show HEAD:tools/card_design.py
git diff HEAD -- tools/card_design.py
```

可以先把历史设计导出为副本进行比较，再决定是否恢复；直接查看历史不会覆盖当前设计。撤销已经推送的提交可用`git revert <提交号>`生成反向提交，避免改写共享历史。

## LFS与连接

```powershell
git lfs install --local
git lfs pull
git lfs ls-files
git lfs fsck
```

PNG及PCK由LFS跟踪；直接下载源码ZIP时，应确认实际资源已包含，否则请使用Git克隆并执行`git lfs pull`。若PNG/PCK是很小的文本指针，尚未下载资源。

本机原有Git配置曾指向未运行的本地代理；此工作目录已设置空的`http.proxy`以直连，未修改全局配置。以后需要代理时，只修改此工作目录的Git配置即可。认证继续使用已有Git Credential Manager账号，不把令牌写进仓库。
