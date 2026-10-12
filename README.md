# 卡哇伊连连看（kawaiiliaochu）

> **本仓库为游戏源码（Unity 工程源码），非打包产物。**
> 抖音小游戏的构建输出（`tt-minigame/`、`tt-minigame-ops/`）已按 `.gitignore` 排除，不入库。

---

## 源码说明

本项目是「卡哇伊连连看 / 图案配对消除」小游戏的 **Unity 源码工程**。

- **引擎**：Unity 2022.3.62f1c1
- **渲染/脚本后端**：IL2CPP + WebGL
- **目标平台**：抖音小游戏（TTSDK 6.9.3，appId `tt0cee7a539296a91002`）
- **屏幕方向**：竖屏（portrait）
- **混淆**：GuardingPear OPS Obfuscator 5.1.0（配置见 `Assets/OPS/Obfuscator/Settings/Obfuscator_Settings.json`）

## 目录结构

| 目录 | 说明 |
|---|---|
| `Assets/Scripts/` | 游戏主体脚本（`Runtime.cs`、`PlayerLocalData.cs`、`View/*`、`GameGrid/*`、`Effect/*`） |
| `Assets/FTools/` | 平台/工具层（Platforms / Network / Cache / Tween / Config） |
| `Assets/AdSDK/` | 抖音广告与排行榜封装 |
| `Assets/Resources/` | 资源（`UI/GameIcons` 共 198 张图标、`ScriptAssets/*.asset` 关卡与主题配置、`View/*.prefab`） |
| `Assets/OPS/` | 代码混淆器及其配置/日志 |
| `ProjectSettings/`、`Packages/` | Unity 工程设置与包清单 |

## 玩法概要

找出两枚相同图案，用不超过两个转角的路线连通即可消除。第 2 关起棋盘会随机旋转、镜像或波浪位移，
边界图案被消除时触发连锁消除，连击最高十倍。

三种模式：

- **关卡模式** —— 按目标分数逐关推进，棋盘尺寸与障碍随进度提升
- **眼力模式** —— 90 秒限时抢分，通关第 5 关解锁
- **记忆模式** —— 翻牌两两配对，配对失误扣体力，比拼最短通关用时

道具：全部翻转 / 精准锁定 / 炸弹。体力上限十点，离线自动恢复。

## 构建

使用字节 Stark SDK 打包为抖音小游戏，输出目录由
`Assets/Editor/StarkBuilderSetting.asset` 的 `OutputDir` 控制。

## 备注

- 上架文案存档见仓库根目录各 `*_作品介绍.txt`。
- UI 绑定依赖「字段名」规则，启用 OPS 混淆时必须关闭字段重命名，详见
  `OPS混淆导致构建包无法打开_诊断与修复.md`。
