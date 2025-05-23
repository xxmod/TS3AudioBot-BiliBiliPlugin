# TS3AudioBot-BiliBiliPlugin

使用TS3AudioBot播放Bilibili音频



## 安装方法

1.进入以下链接下载编译好的插件

[下载插件](https://github.com/xxmod/TS3AudioBot-BiliBiliPlugin/raw/refs/heads/main/bin/Release/netcoreapp3.1/BilibiliPlugin.dll)

2.将插件放置于TS3AudioBot的Plugins目录下

3.下载并将Newtonsoft.json.dll放与TS3AudioBot根目录

[Newtonsoft.json.dll](https://github.com/xxmod/TS3AudioBot-BiliBiliPlugin/raw/refs/heads/main/bin/Release/netcoreapp3.1/Newtonsoft.Json.dll)

4.在right.toml中添加cmd.bilibili cmd.bilibili.bv cmd.bilibili.p cmd.bilibili.login权限



## 使用方法

1.私聊机器人使用`!plugin load []`命令加载插件

2.使用`!bilibili bv [BV号]`播放对应视频的音频

3.使用`!bilibili login SESSDATA=[SESSDATA];` 用cookie登录

