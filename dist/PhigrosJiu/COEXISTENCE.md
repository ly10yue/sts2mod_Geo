# PhigrosJiu 与 jiu_2 同载约定

主版从已接通的可玩结构独立发展，现有 85 张正式填写效果的卡牌，以多牌、透支、信号为机制。两边为独立 Mod，无相互依赖；不将主版卡牌代码覆盖进 jiu_2。

| 项目 | 主版 | 对照版 |
| --- | --- | --- |
| 源码目录 | `.modding/PhigrosJiu` | `.modding/jiu_2` |
| 安装目录 | `mods/PhigrosJiu` | `mods/jiu_2` |
| Manifest ID / 程序集 | `PhigrosJiu` | `jiu_2` |
| C# 命名空间 | `PhigrosJiu` | `jiu_2` |
| Harmony 标识 | `local.phigrosjiu.character` | `local.jiu_2.character` |
| 角色模型 ID | `PHIGROS_JIU_GEOPELIA` | `GEOPELIA` |
| 内容 ID 前缀 | `PHIGROS_JIU_` | 无此前缀 |
| PCK 资源根 | `res://PhigrosJiu/` | `res://jiu_2/` |
| 卡池 Title | `phigrosjiu` | `geopelia` |
| 中文选人名称 | 鸠 · 主版 | 鸠 |

## 修改规则

1. 主版在 `tools/card_design.py` 修改设计并构建，保留稳定类名；已有类名决定模型 ID，开始存档后不要随意改名。不要直接修改生成的 C# 或 JSON 后再运行生成器。
2. 主版新增模型自动获得 `PHIGROS_JIU_` 前缀。身份、卡牌及资源补丁按程序集判断，仅处理主版类型，不能改成按 `Geopelia*` 字符串匹配所有程序集。0.5.1的流程兼容补丁额外精确识别程序集 `jiu_2` 且角色ID为 `GEOPELIA` 的对照角色，只处理原版专属Epoch检查；两版的模型ID和统计仍独立。
3. 主版文本键使用此前缀；PowerVar 等动态变量名继续匹配类型名，不能因为模型 ID 加了前缀就改动文本中的动态变量占位符。
4. 新资源放在 `res://PhigrosJiu/`。能力、遗物图标辅助函数会去除模型前缀后找对应文件。
5. 不要把另一份 jiu_2.json 或 jiu_2.dll 放进主版安装目录。游戏递归寻找 manifest，备份应放在 mods 之外。
6. 两个构建脚本各自只写自己的安装目录。主版重新构建不再从对照版覆盖卡牌代码，后续设计可以独立发展。

## 检查

执行 `check-coexistence.ps1`，在 .NET 9 中以两个加载顺序对真实 DLL 打补丁，检查模型 ID、原版 ID、角色列表、牌组、卡池、遗物、能力、卡图、动态变量和模型克隆；再比较两个真实 PCK 及中英文 8 对文本表，要求无路径或文本键冲突。

`build.ps1` 在暂存目录通过资源和同载检查后才安装主版。可指定 `-DotNetHost` 使用带 .NET 9 运行时的 dotnet.exe；游戏自带 Harmony 不支持 .NET 10 运行时，但可用现有 .NET 10 SDK 编译 net9.0。

## 备份与验证记录

`coexistence-baseline.json` 保存备份位置和 jiu_2 的迁移前哈希。旧预览工程与安装包保存在游戏目录的 `.modding-backups/PhigrosJiu-before-coexistence-*`，不参与 Mod 扫描。

首次迁移时，两个加载顺序各通过 296 项托管检查，62 个模型 ID 不冲突，并核对了 jiu_2 当时的 26 个文件。此为历史记录，之后 jiu_2 可独立更新，原哈希清单不代表当前版本必须保持原样。

0.4.0 在两个加载顺序下检查实际 DLL 和 PCK，核对全部模型、85 张主版卡及升级、角色列表、资源与文本键。85 张牌改为歌曲名，保留原模型 ID 与曲绘引用；只修改主版。原生隔离游戏进程同时加载两版，完成主菜单、170 次单牌结算、32 次资源净值核对、专项联动和 960 回合控制场景。具体结果见 `VALIDATION.md`、`ECONOMY.md` 和 `build/native-validation.json`。未进行完整战役或通关测试，也未排查全部第三方 Mod。

0.5.0：85张曲绘与歌曲逐一对应，主版使用独立复制的jiu_2人物资源并缩至85%。原版jiu_2资源及尺寸不变。移除27张牌的消耗，保留内部模型ID；新的原生加载、图名匹配、模型尺寸与双向同载结果见VALIDATION.md。

0.5.1：主版为两版鸠补齐涅奥、建筑师对话，并避开不存在的原版角色专属Epoch。主版启用时对照版一并受益，但不改写对照版文件，也不改变卡牌效果。独立宿主对两版各验证普通/精英/首领完整结算与奖励，以及建筑师最终选项；对照版安装文件逐一与本轮开始时的SHA-256比较。
