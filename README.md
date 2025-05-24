# TS3AudioBot-BiliBiliPlugin

使用TS3AudioBot播放Bilibili音频



## 安装方法

1.进入以下链接下载编译好的[插件](https://github.com/xxmod/TS3AudioBot-BiliBiliPlugin/blob/main/bin/Release/netcoreapp3.1/BilibiliPlugin.dll)

2.将插件放置于TS3AudioBot的Plugins目录下

3.下载并将[Newtonsoft.json.dll](https://github.com/xxmod/TS3AudioBot-BiliBiliPlugin/raw/refs/heads/main/bin/Release/netcoreapp3.1/Newtonsoft.Json.dll)放与TS3AudioBot根目录

4.在right.toml中添加`cmd.bilibili` `cmd.bilibili.bv` `cmd.bilibili.p` `cmd.bilibili.login` `cmd.bilibili.history` `cmd.bilibili.h`权限



## 使用方法

1.私聊机器人使用`!plugin load [插件编号]`命令加载插件，插件编号通过`!plugin lists`查询

2.使用`!bilibili bv [BV号]`播放对应视频的音频

3.使用`!bilibili login SESSDATA=[SESSDATA]; bili_jct=[bili_jct]` 用cookie登录

4.在登录后可以使用,`!bilibili histroy`查看历史最近十条播放记录



## 编译源文件

1.下载[netcoreapp3.1sdk](https://dotnet.microsoft.com/zh-cn/download/dotnet/thank-you/sdk-3.1.426-windows-x64-installer)

2.clone[本库](https://github.com/xxmod/TS3AudioBot-BiliBiliPlugin/archive/refs/heads/main.zip)

3.运行release.bat



#### SESSDATA与bili_jct查询方法

1.正常登录网页版bilibili

2.右键网页检查选择应用程序

3.Cookie下的https://message.bilibili.com 中有名为SESSDATA与bili_jct的值即需要的数据

