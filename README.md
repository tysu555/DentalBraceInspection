# 牙套尺寸测量系统 (Dental Brace Dimension Inspection)

基于 `牙套.hdev` 中现有 HALCON 算法，使用 **C# WPF (.NET Framework 4.8)** 二次开发的工业产线尺寸测量上位机界面。

## 一、功能

| 模块 | 说明 |
|------|------|
| Halcon 图像显示窗口 | `HSmartWindowControlWPF`，叠加显示工件轮廓(黄)与引脚(绿) |
| 测量结果显示 | 工件总数(Number)、良品数(Number1)、不良数、引脚数、耗时 |
| OK/NG 判定 | 大号 OK/NG 横幅，绿/红高亮 |
| 设备启停 | 启动 / 停止 / 手动触发，运行状态指示灯 |
| 参数配置面板 | 算法全部参数 + 图像源/节拍，支持保存 / 加载 / 恢复默认 / 应用 |
| 运行日志 | 实时滚动，分级着色，写入 `Logs\yyyy-MM-dd.log` |
| 历史数据表格 | 每条检测记录，支持导出 CSV、清空、良率统计 |

## 二、算法映射 (对应 牙套.hdev)

```
read_image → rgb1_to_gray → median_image → threshold(0,125)
→ closing_rectangle1(40,40) → connection → select_shape(area 200..16000)
→ count_obj                       ⇒ TotalCount  (Number : 工件总数)

fill_up → connection → select_shape(area/width/height)
→ select_shape(area 7000..10000) → count_obj
                                  ⇒ GoodCount   (Number1 : 有引脚良品数)

对每个良品区域: opening_circle(20) → difference → connection
                → select_shape_std(max_area, 70)  ⇒ 引脚区域
```

判定规则：`工件总数 > 0 且 不良数 ≤ 允许NG数` 时判 OK，否则 NG。

## 三、目录结构

```
DentalBraceInspection.csproj     项目文件 (net48 + HALCON 引用)
app.manifest                     DPI 感知清单
App.xaml / App.xaml.cs           应用入口, HALCON 运行时初始化
MainWindow.xaml / .xaml.cs       主界面 (布局 + 交互逻辑)
Themes/IndustrialTheme.xaml      工业风深色主题
Converters/LogLevelToBrushConverter.cs
Models/                          InspectionParameters / InspectionResult / HistoryRecord / LogEntry
Services/                        HalconEnvironment / HalconInspectionService
                                 ImageSource / InspectionController / Logger / ConfigStore
```

## 四、环境要求

- **HALCON 24.11**（本机路径 `C:\Program Files\MVTec\HALCON-24.11-Progress-Steady`）
- **.NET Framework 4.8** + WPF
- Visual Studio 2019/2022 或 .NET SDK (`dotnet build`)

## 五、编译运行

```powershell
# 命令行编译
dotnet build "DentalBraceInspection.csproj" -c Debug

# 运行
.\bin\Debug\net48\DentalBraceInspection.exe
```

或用 Visual Studio 直接打开 `.csproj` 按 F5。

> **必须使用 x64**：项目已固定 `PlatformTarget=x64`，与 HALCON x64 运行时一致。

## 六、HALCON 引用与运行时

- 引用的是 HALCON 自带的 .NET 绑定：
  `C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\bin\dotnet35\halcondotnet.dll`
  （包含 `HSmartWindowControlWPF`）。
- 若安装路径不同，请修改 `DentalBraceInspection.csproj` 中 `<HintPath>`。
- 程序启动时 `HalconEnvironment.EnsureInitialized()` 会自动定位 HALCON 安装目录，
  并把 `bin\x64-win64` 加入 `PATH`，保证 native `halcon.dll` 能被加载。

## 七、参数说明

| 界面字段 | Halcon 算子参数 | 默认 |
|----------|-----------------|------|
| 中值滤波半径 | median_image radius | 3 |
| 阈值上限 | threshold max | 125 |
| 闭运算宽/高 | closing_rectangle1 | 40 / 40 |
| 筛选面积下限/上限 | select_shape area | 200 / 16000 |
| 预筛面积/宽/高 | select_shape area,width,height | 150..10000 / 50..150 / 50..150 |
| 终筛面积下限/上限 | select_shape area | 7000 / 10000 |
| 引脚开运算半径 | opening_circle | 20 |
| 引脚最大面积 | select_shape_std max_area | 70 |

图像源支持 **Folder**（文件夹循环取图，支持递归子目录）与 **Single**（单张图像）。
默认文件夹 `D:\HAlianxi\images\blob\找出牙套的长脚`（即 `牙套.hdev` 所用的数据集目录），
可在“参数配置 → 图像文件夹”中修改后点击“应用并检测”。

> 注意：不要把图像文件夹设为 `D:\HAlianxi\images` 根目录，其中包含大量其它练习的图片，
> 会导致循环检测到无关图像。请指向具体的数据集子目录。

## 八、常见问题

1. **提示“未找到 HALCON 运行时”**：确认已安装 HALCON，并设置系统环境变量
   `HALCONROOT`，或通过 `HalconEnvironment` 自动扫描。
2. **`DllNotFoundException: halcon`**：确保 HALCON 的 `bin\x64-win64` 已加入 `PATH`，
   且进程为 64 位。
3. **启动后无图像**：在“参数配置 → 图像文件夹”填入包含图片的目录，点击“应用并检测”。

## 九、接入真实相机

`Services/ImageSource.cs` 中的 `IImageSource` 为取像抽象，替换 `FolderImageSource`
为相机 SDK 实现（如取流→保存/HImage）即可接入实际产线相机，其余逻辑无需改动。
