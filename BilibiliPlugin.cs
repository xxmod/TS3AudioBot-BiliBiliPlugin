using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TS3AudioBot;
using TS3AudioBot.Audio;
using TS3AudioBot.CommandSystem;
using TS3AudioBot.Plugins;

public class BilibiliPlugin : IBotPlugin
{
    private readonly PlayManager _playManager;
    private static readonly HttpClient http = new HttpClient();
    private static string cookieFile = "bili_cookie.txt";
    private static string savedBvid = "";
    private static JArray savedPages;

    public BilibiliPlugin(PlayManager playManager)
    {
        _playManager = playManager;
        LoadCookie();
    }

    public void Initialize() { }
    public void Dispose() { }

    private void LoadCookie()
    {
        if (File.Exists(cookieFile))
        {
            string cookie = File.ReadAllText(cookieFile);
            if (!string.IsNullOrWhiteSpace(cookie))
            {
                http.DefaultRequestHeaders.Remove("Cookie");
                http.DefaultRequestHeaders.Add("Cookie", cookie);
            }
        }
    }

    [Command("bilibili login")]
    public async Task<string> BilibiliLogin(InvokerData invoker, string cookie)
    {
    if (string.IsNullOrWhiteSpace(cookie))
        return "用法: !bilibili login [SESSDATA=xxx; bili_jct=xxx;...]";

    // 保存并设置 Cookie
    File.WriteAllText(cookieFile, cookie);
    http.DefaultRequestHeaders.Remove("Cookie");
    http.DefaultRequestHeaders.Add("Cookie", cookie);

    try
    {
        string userJson = await http.GetStringAsync("https://api.bilibili.com/x/web-interface/nav");
        JObject userObj = JObject.Parse(userJson);
        string uname = userObj["data"]?["uname"]?.ToString();

        if (!string.IsNullOrEmpty(uname))
            return $"登录成功，当前账号：{uname}";

        return "Cookie 已设置，但未能确认登录状态。请检查 Cookie 是否有效。";
    }
    catch (Exception ex)
    {
        return "登录状态确认失败：" + ex.Message;
    }
    }


    [Command("bilibili bv")]
    public async Task<string> BilibiliBvCommand(InvokerData invoker, string bvid)
    {
        if (string.IsNullOrWhiteSpace(bvid))
            return "请提供 BV 号，例如：!bilibili bv BV1xK4y1a7Yx";

        try
        {
            string viewApi = $"https://api.bilibili.com/x/web-interface/view?bvid={bvid}";
            string viewJson = await http.GetStringAsync(viewApi);
            JObject viewData = JObject.Parse(viewJson)["data"] as JObject;

            if (viewData == null)
                return "未获取到视频信息，请检查 BV 号是否正确。";

            JArray pages = viewData["pages"] as JArray;
            savedBvid = (string)viewData["bvid"];
            savedPages = pages;

            if (pages != null && pages.Count > 1)
            {
                string reply = $"视频包含 {pages.Count} 个分P：\n";
                for (int i = 0; i < pages.Count; i++)
                {
                    reply += $"{i + 1}. {pages[i]["part"]}\n";
                }

                reply += "\n请使用命令 !bilibili p [编号] 播放对应分P。";
                return reply;
            }

            // 无分P情况默认播放第一集
            long cid = (long)viewData["cid"];
            return await PlayAudio(cid, bvid, invoker);
        }
        catch (Exception ex)
        {
            return "获取视频信息时出错：" + ex.Message;
        }
    }

    [Command("bilibili p")]
    public async Task<string> BilibiliPlayPart(InvokerData invoker, int partIndex)
    {
        if (savedPages == null || savedPages.Count == 0 || string.IsNullOrWhiteSpace(savedBvid))
            return "请先使用 !bilibili bv [BV号] 获取视频信息。";

        if (partIndex < 1 || partIndex > savedPages.Count)
            return $"请输入有效编号（1 - {savedPages.Count}）。";

        JObject page = savedPages[partIndex - 1] as JObject;
        long cid = (long)page["cid"];
        return await PlayAudio(cid, savedBvid, invoker);
    }

    private async Task<string> PlayAudio(long cid, string bvid, InvokerData invoker)
    {
    try
    {
        string playApi = $"https://api.bilibili.com/x/player/playurl?cid={cid}&bvid={bvid}&fnval=16&fourk=1";
        string playJson = await http.GetStringAsync(playApi);
        JArray audioArray = JObject.Parse(playJson)["data"]?["dash"]?["audio"] as JArray;

        if (audioArray == null)
            return "未能获取音频流地址，视频可能不支持 DASH 音频。";

        JObject bestAudio = audioArray.OrderByDescending(a => (long)a["bandwidth"]).FirstOrDefault() as JObject;
        if (bestAudio == null)
            return "未能获取有效的音频链接。";

        // 构建 URL 和来源名的列表
        var urlSources = new List<(string Url, string Type)>
        {
            ((string)bestAudio["baseUrl"], "baseUrl"),
            ((string)bestAudio["base_url"], "base_url")
        };

        if (bestAudio["backupUrl"] is JArray backupUrls)
            urlSources.AddRange(backupUrls.Select(u => (u.ToString(), "backupUrl")));

        if (bestAudio["backup_url"] is JArray backupUrls2)
            urlSources.AddRange(backupUrls2.Select(u => (u.ToString(), "backup_url")));

        // 依次尝试播放每个 URL
        foreach (var (url, type) in urlSources)
        {
            if (string.IsNullOrWhiteSpace(url)) continue;
            try
            {
                await _playManager.Enqueue(invoker, url);
                Console.WriteLine($"播放成功：{type} - {url}");
                return $"正在播放 B站 视频 {bvid} 的音频（分P cid={cid}）。";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"播放失败：{type} - {url}\n原因: {ex.Message}");
                // 尝试下一个
            }
        }

        return "所有音频链接播放失败。";
    }
    catch (Exception ex)
    {
        return "播放失败：" + ex.Message;
    }
    }

}