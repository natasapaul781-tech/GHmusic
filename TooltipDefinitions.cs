namespace Soundboard;

public static class TooltipDefinitions
{
    public static Dictionary<string, string> All { get; } = new()
    {
        ["ptt_key"] = "语音激活键 (Push-to-Talk)\n在游戏中按住此键说话。播放音效时会自动模拟按下此键，让你的队友听到音乐。\n默认: V",
        ["play_key"] = "播放触发键\n按下此键开始播放当前选中的音效到队内语音频道。\n默认: P",
        ["stop_key"] = "播放终止键\n按下此键立即停止所有正在播放的音频。\n默认: Ctrl+S",

        ["play_duration"] = "播放时长\n设定每次触发后音频的最大播放时长（秒）。超时自动停止。\n范围: 1-60 秒，推荐: 15 秒",
        ["allow_overlap"] = "允许重叠播放\n启用后可以同时播放多个音效；禁用后新播放会自动停止之前的播放。",
        ["ptt_enable"] = "启用 PTT 自动按键\n播放音效时自动模拟按下语音键，让队友听到音乐。\n关闭则只在你手动按语音键时队友才能听到。",
        ["ptt_mode"] = "PTT 持续模式\n手动开启后会持续模拟按住语音键，就像你在游戏中一直按着语音键一样。\n适合需要长时间开麦的场景。\n关闭则释放语音键。",
        ["mic_passthrough"] = "麦克风混音\n启用后会自动捕获物理麦克风的声音，混入到游戏输出设备（虚拟麦克风）中。\n即使使用 VB-Cable 等虚拟麦克风，队友也能听到你说话的声音。\n注意：请确保游戏中的麦克风设备设置为 CABLE Output。",
        ["start_minimized"] = "启动时自动最小化\n程序启动后自动隐藏到系统托盘，不打扰游戏体验。",

        ["game_output"] = "游戏输出设备\n选择音频输出到的设备。通常选择 VB-Cable (CABLE Input)，然后在游戏设置中将麦克风设为 CABLE Output。",
        ["local_output"] = "本地试听设备\n选择你用来监听音乐的设备（耳机/音箱），这样你自己也能听到播放的音乐。",
        ["mic_output"] = "麦克风输出设备\n选择麦克风线路设备，用于 PTT 自动按键时保持麦克风信号路径。",

        ["game_volume"] = "游戏音量\n控制输出到游戏语音频道的音量大小（队友听到的音量）。\n推荐: 60-80%",
        ["local_volume"] = "本地音量\n控制你自己监听时的音量大小。\n推荐: 100%",
        ["mic_volume"] = "麦克风音量\n控制 PTT 时麦克风信号的音量大小。\n推荐: 100%",

        ["btn_save"] = "保存当前所有配置到本地文件。\n配置保存在: %AppData%\\Soundboard\\config.json",
        ["btn_stop_all"] = "立即停止所有正在播放的音频。\n快捷键: ",
        ["vb_cable"] = "VB-Audio Virtual Cable 虚拟声卡\n安装后会在系统中创建虚拟音频设备，让你可以把音乐路由到游戏的语音频道。\n点击开始自动下载安装。",

        ["card_add"] = "添加音效文件\n支持 MP3、WAV、AIFF、AAC、M4A 格式。\n也可以直接拖拽音频文件到此处。",
        ["card_empty"] = "欢迎使用 Soundboard！\n\n还没有添加任何音效。\n\n👉 拖拽音频文件到此处\n👉 或点击 + 号按钮选择文件\n👉 右键卡片可设置热键和裁剪区间",
    };
}
