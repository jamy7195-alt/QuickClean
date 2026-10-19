# QuickClean · Windows 快捷安全清理工具

一个单文件、免安装的 Windows 清理工具，提供「硬盘清理」和「内存优化」两大功能。
使用 C# + WinForms 编写，`.NET 8` 单文件自包含发布，目标机器**无需安装任何运行时**，双击即用。

> **交付物只有一个文件**：`publish\QuickClean.exe`（59.7 MB）。
> 它是**真正独立**的单文件——不需要同目录任何 DLL，也**不依赖 `%TEMP%` 可写**。

---

## 一、功能

### 1. 硬盘清理
- 清理用户临时文件 `%TEMP%`
- 清理系统临时文件 `C:\Windows\Temp`（需管理员）
- 清理 Chrome / Edge 浏览器缓存（需先关闭对应浏览器）
- 清空回收站
- 清理 Windows 更新缓存（`C:\Windows\SoftwareDistribution\Download`，需管理员）
- 清理缩略图缓存（`thumbcache_*` / `iconcache_*`）
- 清理崩溃转储 / Windows 错误报告缓存（`CrashDumps` / `WER`）

### 2. 内存优化
- 实时显示物理内存总量 / 已用 / 可用（每 2 秒刷新）
- 一键释放：清空非保护进程的工作集（Working Set），前后内存对比
- 列出「无响应」进程，可勾选后结束

### 3. 交互
- 图形界面，一窗完成
- 两种模式：**快捷模式**（一键扫描并清理默认安全项）与 **自定义模式**（勾选要清理的项目）
- 扫描前先显示每项**可释放空间**，再决定是否清理
- 全程进度条 + 状态提示，结束后输出**清理报告**（可保存 / 复制）

---

## 二、首次运行：Windows SmartScreen 提示

因为本工具**没有商业代码签名证书**，Windows 会弹出蓝色提示：

> **Windows 已保护你的电脑**
> Microsoft Defender SmartScreen 阻止了无法识别的应用启动。

**这不是程序有问题**，只是 SmartScreen 对「无签名、无下载量记录」的新程序的一律拦截。放行方法：

1. 点击弹窗里的 **「更多信息」**；
2. 右下角出现 **「仍要运行」** 按钮，点它即可。

> 只需放行一次，之后双击就能直接打开。
> 若按钮显示的是「不运行 / 关闭」，说明没点到「更多信息」展开。

如果右键属性里有「**解除锁定**」复选框，勾上并确定也可以（本 exe 通常没有，因为我们没有给它加网络下载标记）。

---

## 三、技术选型说明（为什么不是 Native AOT）

需求最初要求 `Native AOT 单文件 ≤15MB`。经查证，**微软官方不支持 WinForms / WPF 的 Native AOT 发布**：

- WinForms/WPF 依赖大量运行时反射与资源加载机制，AOT 会剪裁掉运行所需元数据，无法工作。
- 直接启用 `PublishTrimmed` 会得到编译器错误 `NETSDK1175：启用剪裁时，不支持或不推荐使用 Windows 窗体`。

因此采用折中方案 **B：WinForms + 单文件自包含（Self-Contained Single-File）**：

| 指标 | Native AOT（不可用于 WinForms） | 本方案（单文件自包含） |
| --- | --- | --- |
| 依赖运行时 | 无 | 无（运行时已打进 exe） |
| 文件形态 | 单个 exe | **单个 exe** |
| 体积 | ~3–5 MB | **59.7 MB**（含完整 .NET 8 运行时，已内部压缩） |
| 启动 | 极快 | 快（**不解压、不写临时目录**） |

如果追求极致体积，唯一可行路线是改用 **Win32 原生 API 手写 GUI**（无 WinForms），可配合 AOT 做到 3–5 MB，但开发量与维护成本显著提高。本工具按你确认的方案 B 实现。

---

## 四、目录结构

| 文件 | 职责 |
| --- | --- |
| `QuickClean.csproj` | 项目配置（WinForms + 单文件自包含发布参数） |
| `app.manifest` | 应用清单（asInvoker 权限 + Win10/11 兼容 + Common Controls v6 依赖） |
| `Program.cs` | 入口；高 DPI、视觉样式、中文字体、全局异常日志 |
| `MainForm.cs` | 主窗口（界面布局 + 全部交互逻辑） |
| `CleanupCatalog.cs` | 清理项目录（8 个清理项的元数据定义） |
| `CleanupEngine.cs` | 清理引擎（扫描大小 + 执行清理 + 生成报告） |
| `CleanupItem.cs` | 清理项数据模型 |
| `RecycleBin.cs` | 回收站查询 / 清空 / 走回收站删除 |
| `MemoryOptimizer.cs` | 工作集释放 + 无响应进程检测 |
| `MemoryInfo.cs` | 物理内存信息（P/Invoke `GlobalMemoryStatusEx`） |
| `NativeMethods.cs` | 所有 Win32 P/Invoke 声明 |
| `ProcessSafety.cs` | 系统关键进程黑名单 |
| `ProtectedPaths.cs` | 文件系统保护路径黑名单 |
| `UnresponsiveForm.cs` | 无响应进程选择对话框 |
| `Util.cs` | 字节数格式化 |

---

## 五、安全设计

### 1. 双重防护（白名单 + 黑名单）
- **白名单（根本保障）**：每个清理项都显式声明要清理的目录（`CleanupCatalog.cs` 中的 `Roots`），引擎**只**在这些目录内操作，绝不越界。
- **黑名单（兜底）**：`ProtectedPaths.cs` 里维护了保护路径清单，任何待删除文件若命中下列路径会直接跳过——
  - 用户个人目录：`Desktop`、`Documents`、`Pictures`、`Music`、`Videos`、`Downloads`、`Favorites`、`Contacts`、`OneDrive`
  - 系统目录：`C:\Windows\{System32, SysWOW64, WinSxS, Boot, inf, assembly, Microsoft.NET, Installer, ...}`
  - 其他：`C:\Program Files`、`C:\Program Files (x86)`、`C:\ProgramData`、`C:\`、`$Recycle.Bin`、`System Volume Information` 等

### 2. 删除前二次确认
- 点击「清理」后弹出确认框，明确列出要清理的项目。
- 默认启用「**删除到回收站**」选项（`FOF_ALLOWUNDO`），误删可恢复；取消勾选则直接物理删除。

### 3. 不执行危险命令
- 全程使用 .NET 文件 API 与 Win32 API，**不调用 `cmd` / `del` / `format` 等外部命令**。
- 遇到被占用 / 无权限的文件会捕获异常并跳过，不中断整体流程。

### 4. 进程保护
- `ProcessSafety.cs` 黑名单保护系统关键进程（`csrss`、`wininit`、`lsass`、`services`、`explorer`、`svchost`、`dwm`、杀软进程等），内存释放与结束进程都会跳过它们。
- 只结束用户显式勾选的「无响应」进程，绝不强杀正常运行中的应用。

### 5. 权限检测
- 检测当前是否管理员身份，在界面显示「管理员」徽标；需要管理员权限的清理项（系统临时目录、更新缓存）在非管理员下会提示并引导以管理员重启。

---

## 六、构建与发布

### 环境
- .NET 8 SDK（`net8.0-windows` + WinForms 工作负载，SDK 自带）
- 已在本机用本地 SDK（无管理员）验证：`dotnet build` 与 `dotnet publish` 均成功

### 发布命令（生成单文件自包含 exe）

```powershell
dotnet publish QuickClean.csproj -c Release -r win-x64 --self-contained true -o publish
```

产物：`publish\QuickClean.exe`（**59.7 MB**，单文件，免运行时）。

> 发布目录里可能附带几个以 `_cor3.dll` 结尾的原生库（WPF/DirectX 相关，由自包含运行时包带入）。
> 实测 **exe 单独即可运行**，这些 DLL 全部可以删除，交付时只留 `QuickClean.exe` 即可。

### csproj 中的关键配置（`QuickClean.csproj`）

```xml
<OutputType>WinExe</OutputType>
<TargetFramework>net8.0-windows</TargetFramework>
<UseWindowsForms>true</UseWindowsForms>

<SelfContained>true</SelfContained>                                  <!-- 运行时打进 exe -->
<PublishSingleFile>true</PublishSingleFile>                          <!-- 单文件 -->
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>  <!-- 内部压缩 -->

<!-- 原生库直接从 bundle 内加载，不再落地到 %TEMP%\.net。
     这样才能在「临时目录不可写」的受限环境下正常启动 -->
<IncludeNativeLibrariesForSelfExtract>false</IncludeNativeLibrariesForSelfExtract>

<DebugType>none</DebugType>                                      <!-- 去符号，减体积 -->
<SatelliteResourceLanguages>zh-Hans</SatelliteResourceLanguages> <!-- 只带中文资源，减体积 -->
```

### 体积优化说明
- `DebugType=none`：不生成 PDB / 调试信息。
- `SatelliteResourceLanguages=zh-Hans`：只打包简体中文资源卫星程序集，省去数十种语言资源。
- `EnableCompressionInSingleFile=true`：对捆绑内容做压缩（59.7 MB 已是压缩后体积）。
- 曾尝试 `PublishTrimmed=true`，但因 WinForms 不支持剪裁而移除（会报 `NETSDK1175`）。
- 未引入任何第三方 NuGet 包（无 Newtonsoft.Json 等），全部使用 .NET 内置 API 与 Win32 P/Invoke。

---

## 七、两个已修复的启动问题（重要）

### 问题 1：`Application.EnableVisualStyles()` 导致启动即崩溃

**现象**：双击后无任何界面，进程几秒内退出；事件日志出现
`出错应用程序名称： QuickClean.exe`、`异常代码： 0xe0434352`（CLR 未处理托管异常）。

**根因**：WinForms 的 `EnableVisualStyles()` 内部会调用
`System.Windows.Forms.ThemingScope.CreateActivationContext(Stream manifest)`，
把一份视觉样式清单**写到 `%TEMP%` 下的临时文件**。当临时目录不可写时抛出：

```
System.UnauthorizedAccessException: Access to the path 'C:\Users\...\AppData\Local\Temp\jkssuwm5.bfz' is denied.
   at System.Windows.Forms.ThemingScope.CreateActivationContext(Stream manifest)
   at System.Windows.Forms.Application.EnableVisualStyles()
   at QuickClean.Program.Main()
```

异常未被捕获 → 进程以 `0xE0434352` 退出。

**修复（双保险，两处都已生效）**：
1. **`app.manifest` 声明 Common Controls v6 依赖**：
   ```xml
   <dependency>
     <dependentAssembly>
       <assemblyIdentity type="win32" name="Microsoft.Windows.Common-Controls"
                         version="6.0.0.0" processorArchitecture="*"
                         publicKeyToken="6595b64144ccf1df" language="*" />
     </dependentAssembly>
   </dependency>
   ```
   系统在加载程序时就直接建立视觉样式激活上下文，`EnableVisualStyles()` 于是**无需再写任何临时文件**。
2. **`Program.cs` 逐项降级**：`SetHighDpiMode` / `EnableVisualStyles` / `SetCompatibleTextRenderingDefault` / `SetDefaultFont`
   全部各自 `try/catch`；即使样式初始化失败，也只是退回经典外观，**程序照常启动**。
   `Application.Run` 外层也有 `try/catch`，真出问题会弹窗告知而不是静默消失。

**验证结果**：在**不做任何 `%TEMP%` 重定向**、且模拟"沙箱外双击"（计划任务）两种情况下，
窗口均正常出现：`Title='QuickClean · Windows 快捷安全清理工具' Responding=True`，且不再产生 `error.log`。

### 问题 2：不再往 `%TEMP%\.net` 解压原生库

原先 `IncludeNativeLibrariesForSelfExtract=true` 会让单文件宿主把原生库解压到 `%TEMP%\.net`，
在受限环境下报 `Failed to create default extraction directory [...\Temp\.net]. error code: 5`。

改为 `false` 后原生库直接从 bundle 加载，**启动更快，也不再需要可写的临时目录**。
代价是 exe 从 63.26 MB 降到 59.7 MB（更小），发布目录多出的几个 `_cor3.dll` 可直接删除。

---

## 八、使用说明

1. 双击 `QuickClean.exe`。若被 SmartScreen 拦下，点「更多信息」→「仍要运行」（见第二节）。
2. 界面顶部显示管理员状态；如需清理系统级缓存，点「以管理员身份重启」。
3. 内存区实时显示内存占用；点「释放内存」清空非保护进程工作集。
4. 硬盘区选「快捷模式」或「自定义模式」，点「扫描」查看各项可释放空间。
5. 勾选要清理的项 → 点「清理」→ 二次确认后执行。
6. 清理完成后可在底部查看报告，点「保存报告」或「复制」。

调试用日志：若程序真的遇到未处理异常，会写到
`%LOCALAPPDATA%\QuickClean\error.log`。**该文件不存在 = 从未发生未处理异常。**

## 九、注意事项

- 清理 Chrome/Edge 缓存前请先关闭对应浏览器，否则文件被占用会跳过。
- 需要管理员权限的清理项在非管理员下不会执行（会在界面提示）。
- 默认「删除到回收站」，可在界面取消勾选改为直接删除。
- 内存释放对「无响应进程」会提示是否结束；结束进程会导致其未保存数据丢失，请谨慎勾选。
- 单文件自包含 exe 体积较大（约 60 MB）属正常，因为已包含完整 .NET 8 运行时。
