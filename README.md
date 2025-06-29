# TS3AudioBot-BiliBiliPlugin

使用TS3AudioBot播放Bilibili音频



## 安装方法

#### 方法1

1.进入以下链接下载并解压编译好的[插件](https://github.com/xxmod/TS3AudioBot-BiliBiliPlugin/blob/main/bin/Release/netcoreapp3.1/BilibiliPlugin.dll)

2.下载并解压编译好的bilibili-referer-proxy[插件](https://github.com/xxmod/Bilibili-Referer-Proxy/releases/download/1.0.0/Proxy-windows.zip)

3.将插件放置于TS3AudioBot的Plugins目录下

4.下载并将[Newtonsoft.json.dll](https://github.com/xxmod/TS3AudioBot-BiliBiliPlugin/raw/refs/heads/main/bin/Release/netcoreapp3.1/Newtonsoft.Json.dll)放与TS3AudioBot根目录

5.在right.toml中添加`cmd.bilibili` `cmd.bilibili.bv` `cmd.bilibili.p` `cmd.bilibili.login` `cmd.bilibili.history` `cmd.bilibili.h` `cmd.bilibili.add` `cmd.bilibili.addh` `cmd.bilibili.qr`权限

6.配置好相关的服务器链接，用户权限等

#### 方法2

1.进入release下载解压最新的release

2.下载解压bilibili-referer-proxy[插件](https://github.com/xxmod/Bilibili-Referer-Proxy/releases/download/1.0.0/Proxy-windows.zip)

3.配置right.toml与bot文件夹

4.使用



## 使用方法

1.打开proxy.exe

2.打开TS3AudioBot.exe

3.私聊机器人使用`!plugin load [插件编号]`命令加载插件，插件编号通过`!plugin lists`查询

4.使用`!bilibili bv [BV号]`播放对应视频的音频

*如有多p会提示使用`!bilibili p [选项]`播放某一p*

5.使用`!bilibili add [BV号]`添加音频到下一个播放

*提示使用`!bilibili addp [选项]`添加某一p到下一个播放*

6.使用`!bilibili login SESSDATA=[SESSDATA]; bili_jct=[bili_jct]` 用cookie登录，每个账号的cookie是独立的

7.在登录后可以使用,`!bilibili histroy`查看历史最近十条播放记录并可选播放某一视频的音频

8.使用!bilibili qr 使用二维码登录



## 编译源文件

1.下载[netcoreapp3.1sdk](https://dotnet.microsoft.com/zh-cn/download/dotnet/thank-you/sdk-3.1.426-windows-x64-installer)

2.clone[本库](https://github.com/xxmod/TS3AudioBot-BiliBiliPlugin/archive/refs/heads/main.zip)

3.运行release.bat



## 感谢

[`bilibili-API-collect`](https://github.com/SocialSisterYi/bilibili-API-collect)提供的bilibiliapi文档
[`ZHANGTIANYAO1/TS3AudioBot-NetEaseCloudmusic-plugin`](https://github.com/ZHANGTIANYAO1/TS3AudioBot-NetEaseCloudmusic-plugin)提供代码案例
[`Splamy/TS3AudioBot`](https://github.com/Splamy/TS3AudioBot)提供的文档和平台





#### SESSDATA与bili_jct查询方法

1.正常登录网页版bilibili

2.右键网页检查选择应用程序

3.Cookie下的https://message.bilibili.com 中有名为SESSDATA与bili_jct的值即需要的数据

