# 🎵 HGmusic - 游戏音效播放助手

<p align="center">
  <img src="image/xiaohonghua.jpg" alt="" width="200" style="border-radius: 20px;">
</p>

<p align="center">
  <strong>在多人在线游戏中，通过自定义热键将音乐或音效播放到队内语音频道</strong>
</p>

<p align="center">
  <a href="#功能特点">功能特点</a> •
  <a href="#快速开始">快速开始</a> •
  <a href="#安装说明">安装说明</a> •
  <a href="#使用指南">使用指南</a> •
  <a href="#配置说明">配置说明</a> •
  <a href="#开发相关">开发相关</a>
</p>

---

## ✨ 功能特点

### 🎮 核心功能
- **自定义音效播放** - 支持 MP3、WAV、AIFF、AAC、M4A 等多种音频格式
- **全局热键触发** - 在游戏中按下快捷键即可播放音效到队内语音
- **独立快捷键** - 每个音效可单独设置快捷键，支持 Ctrl、Shift、Alt 修饰键
- **实时音频注入** - 通过 VB-Cable 虚拟声卡将音频注入游戏语音频道

### 🎤 语音功能
- **PTT 自动按键** - 播放音效时自动模拟按住语音键（Push-to-Talk）
- **PTT 持续模式** - 开启后保持语音键按下状态，实现持续开麦
- **麦克风混音** - 将物理麦克风声音混入虚拟麦克风，队友也能听到你说话
- **低延迟传输** - 优化的音频缓冲设置，延迟约 150-200ms

### 🎛️ 音频控制
- **多设备输出** - 同时输出到游戏设备和本地监听设备
- **独立音量控制** - 游戏音量、本地音量、麦克风音量独立调节
- **播放时长限制** - 可设置 1-60 秒的播放时长
- **音频区间裁剪** - 支持设置音频的起止时间，只播放片段
- **重叠播放** - 可选择是否允许多个音效同时播放

### 💾 预设管理
- **多预设支持** - 创建不同的音效预设方案
- **快速切换** - 一键切换不同的音效配置
- **预设复制** - 基于现有预设创建新方案

### 🎨 界面特性
- **现代化 UI** - 深色主题，圆角卡片设计
- **响应式布局** - 支持窗口缩放，最小尺寸自适应
- **音频波形显示** - 可视化音频文件的波形
- **托盘最小化** - 最小化到系统托盘，不占用任务栏

---

## 🚀 快速开始

### 系统要求
- Windows 10/11
- .NET 8.0 Runtime
- VB-Cable 虚拟声卡（用于音频注入）

### 3 分钟快速上手

1. **下载并运行** 程序
2. **首次启动** 会显示设置向导
3. **导入音效** - 点击「选择音频文件」添加你喜欢的音效
4. **设置热键** - 按下想要的快捷键组合
5. **安装 VB-Cable** - 点击侧边栏的「安装 VB-Cable」按钮
6. **配置游戏** - 在游戏中将麦克风设备设置为「CABLE Output」
7. **开始使用** - 按下热键，队友就能听到你的音效了！

---

## 📦 安装说明

### 方式一：直接运行
1. 从 Releases 页面下载最新版本
2. 解压到任意目录
3. 运行 `Soundboard.exe`

### 方式二：从源码构建
```bash
# 克隆仓库
git clone https://github.com/yourusername/soundboard.git
cd soundboard

# 构建项目
dotnet build -c Release

# 运行程序
dotnet run
```

### 安装 VB-Cable
1. 访问 [VB-Audio 官网](https://vb-audio.com/Cable/) 下载 VB-Cable
2. 以管理员身份运行安装程序
3. 安装完成后重启电脑
4. 在 Windows 声音设置中确认出现「CABLE Input」和「CABLE Output」设备

---

## 📖 使用指南

### 基本使用

#### 添加音效
1. 点击主界面右上角的「+」按钮
2. 选择音频文件（支持多选）
3. 音效卡片会显示在主界面中

#### 设置快捷键
1. 右键点击音效卡片
2. 选择「设置快捷键」
3. 按下想要的按键组合
4. 支持的修饰键：Ctrl、Shift、Alt

#### 播放音效
- **鼠标点击**：点击音效卡片的播放按钮
- **快捷键**：按下设置的快捷键
- **全局触发键**：按下侧边栏设置的「播放触发键」

### 高级功能

#### PTT 自动按键
- 启用后，播放音效时会自动模拟按住语音键
- 适合需要按住才能说话的游戏设置

#### PTT 持续模式
- 开启后保持语音键按下状态
- 适合需要持续开麦的场景
- 状态栏会显示「PTT:持续开麦中」

#### 麦克风混音
- 启用后会捕获物理麦克风的声音
- 混入到虚拟麦克风（VB-Cable）中
- 队友既能听到音效，也能听到你说话

#### 音频区间裁剪
1. 右键点击音效卡片
2. 选择「设置播放区间」
3. 拖动滑块设置起止时间
4. 只播放选定的片段

### 设备配置

#### 推荐配置
```
游戏输出设备：CABLE Input (VB-Cable)
本地监听设备：扬声器/耳机
麦克风输出设备：CABLE Input (VB-Cable)
```

#### 游戏内设置
1. 打开游戏的音频设置
2. 将麦克风/语音输入设备设置为「CABLE Output」
3. 确保语音模式设置为「按键说话」（如果使用 PTT 功能）

---

## ⚙️ 配置说明

### 配置文件位置
```
%APPDATA%/Soundboard/
├── 默认预设/
│   └── config.json
├── 预设1/
│   └── config.json
└── 预设2/
    └── config.json
```

### 配置项说明

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `PlayTriggerKey` | int | 80 (P) | 播放触发键的虚拟键码 |
| `PlayTriggerModifiers` | int | 0 | 播放触发键的修饰键 |
| `StopKey` | int | 83 (S) | 停止键的虚拟键码 |
| `StopModifiers` | int | 2 (Ctrl) | 停止键的修饰键 |
| `PttKey` | int | 86 (V) | PTT 键的虚拟键码 |
| `PttModifiers` | int | 0 | PTT 键的修饰键 |
| `PlayDurationSeconds` | int | 15 | 默认播放时长（秒） |
| `GameVolume` | float | 1.0 | 游戏输出音量（0-1） |
| `LocalVolume` | float | 1.0 | 本地监听音量（0-1） |
| `MicVolume` | float | 1.0 | 麦克风音量（0-1） |
| `PttEnable` | bool | true | 是否启用 PTT 自动按键 |
| `MicPassthrough` | bool | false | 是否启用麦克风混音 |
| `AllowOverlap` | bool | true | 是否允许重叠播放 |
| `StartMinimized` | bool | false | 是否启动时最小化 |

---

## 🛠️ 开发相关

### 技术栈
- **框架**：.NET 8.0 + Windows Forms
- **音频库**：NAudio 2.2.1
- **虚拟声卡**：VB-Cable

### 项目结构
```
Soundboard/
├── MainForm.cs              # 主窗体
├── SetupWizardForm.cs       # 设置向导
├── AudioEngine.cs           # 音频引擎
├── KeyHookManager.cs        # 键盘钩子管理
├── InputSimulator.cs        # 输入模拟器
├── AppConfig.cs             # 配置管理
├── HotkeyBinding.cs         # 快捷键绑定
├── SoundCard.cs             # 声卡管理
├── VBCableManager.cs        # VB-Cable 管理
├── WaveformRenderer.cs      # 波形渲染器
├── WaveformTrimForm.cs      # 波形裁剪窗体
├── PresetManagerForm.cs     # 预设管理窗体
├── CharityDonationForm.cs   # 捐赠窗体
├── AnimatedButton.cs        # 动画按钮控件
├── ThemedControls.cs        # 主题控件
├── Theme.cs                 # 主题配置
└── TooltipDefinitions.cs    # 工具提示定义
```

### 构建与运行
```bash
# 调试模式
dotnet run

# 发布版本
dotnet publish -c Release -r win-x64 --self-contained
```

### 依赖项
```xml
<PackageReference Include="NAudio" Version="2.2.1" />
```

---

## 🐛 常见问题

### Q: 为什么队友听不到我的音效？
A: 请检查：
1. VB-Cable 是否已安装
2. 游戏中麦克风设备是否设置为「CABLE Output」
3. 侧边栏的「麦克风输出」是否选择「CABLE Input」

### Q: 快捷键没反应怎么办？
A: 请检查：
1. 快捷键是否与其他程序冲突
2. 程序是否以管理员身份运行（某些游戏需要）
3. 键盘钩子是否正常工作（状态栏显示）

### Q: 音效有延迟怎么办？
A: 可以尝试：
1. 降低播放时长设置
2. 使用更小的音频文件
3. 关闭不需要的音频处理功能

### Q: 如何恢复默认设置？
A: 删除配置目录即可：
```
%APPDATA%/Soundboard/
```

---

## 📄 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件

---

## 🙏 致谢

- [NAudio](https://github.com/naudio/NAudio) - .NET 音频库
- [VB-Audio](https://vb-audio.com/) - 虚拟音频电缆

---

<p align="center">
  Made with ❤️ for gamers
</p>
