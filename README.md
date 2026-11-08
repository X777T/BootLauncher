# BootLauncher

一个 Windows 桌面启动器：把常用的应用和文件夹摆成图标网格，勾选后一键同时启动。适合开机后一次性打开固定的一组工作环境。

A small Windows launcher. Lay out your frequently used apps and folders as an icon grid, tick the ones you want, and start them all with one click.

## 功能

- **图标网格**：深色界面，滚动平滑，悬停、勾选、拖拽排序都有动画。
- **一键启动**：勾选应用后点“启动”，按顺序依次打开。按钮上显示已选数量，没选时置灰。
- **方案**：把一组勾选保存成命名方案，之后一键切换。“默认”方案不预选任何应用。方案可以在“管理”里重命名、删除、添加，最多 10 个。
- **分类**：自建标签给应用分类，也可以完全不分类。按分类筛选；给分类添加应用时，从已添加的应用里挑，并标出它们当前所属的分类。
- **搜索**：顶栏搜索框，搜索后可点“返回”回到完整列表。
- **自动扫描**：点“更新”扫描本机已安装的应用，批量添加。
- **其它**：图标大小可调、开机自启、中英文界面（跟随系统或手动选择）、支持高 DPI。

## 使用

1. 点网格里的 **+** 添加应用，选一个程序、快捷方式或文件夹；也可以点“更新”自动扫描。
2. 点图标勾选要启动的应用，拖动图标可以调整顺序。
3. 点底部的 **启动**，或按 `Enter`。
4. 想固定一组常用组合：勾选后点方案条上的 **+**，先确认应用，再给方案起名并保存。
5. 想分类：右键应用，选“分类”。或者点分类条上的 **+** 新建分类。

右键应用图标可以编辑、改分类、删除。右键方案或分类标签可以重命名、删除。

## 快捷键

| 按键 | 作用 |
| --- | --- |
| `Enter` | 启动已勾选的应用 |
| `Esc` | 有搜索内容时先清空搜索，否则关闭窗口 |
| `Ctrl+F` | 聚焦搜索框 |
| `Ctrl+A` | 全选 / 取消全选 |

## 运行环境

- Windows 10（1903 及以上）或 Windows 11，已自带 .NET Framework 4.8。
- Windows 11 上窗口使用系统圆角；Windows 10 上自动回退为程序自绘的圆角。

## 构建

项目不依赖任何第三方包。用 Visual Studio 打开 `BootLauncher.csproj` 直接生成，或在命令行运行：

```
msbuild BootLauncher.csproj /p:Configuration=Release
```

产物在 `bin/Release/BootLauncher.exe`。

## 配置与数据

- 设置保存在 `%APPDATA%\BootLauncher\config.json`，包括应用列表、方案、分类、窗口位置和语言。删掉这个文件即可恢复初始状态。
- 开机自启通过当前用户启动文件夹里的快捷方式实现，不写注册表，不需要管理员权限。
- 程序不联网，不收集任何数据。

## 目录结构

```
Program.cs            入口
MainForm.cs           主窗口：布局、方案、分类、启动流程
AppGrid.cs            应用网格（自绘、滚动、动画、拖拽排序）
Chrome.cs             顶栏控件：窗口按钮、胶囊标签、搜索框
UiKit.cs              主题色和绘制、动画工具
DarkForm.cs           对话框基类、圆角输入框、深色菜单
AddSchemeForm.cs      添加方案（选应用，再命名）
SchemeManageForm.cs   方案管理
AppEditForm.cs        添加 / 编辑应用
AppPickForm.cs        给分类选应用
ScanAppsForm.cs       扫描本机应用
PromptForm.cs         单行输入对话框
AppScanner.cs         枚举已安装应用
ShellIcon.cs          读取系统大图标
AppConfig.cs          配置读写
AutoStart.cs          开机自启
Strings.cs            中英文文案
```

## 许可证

[MIT](LICENSE)
