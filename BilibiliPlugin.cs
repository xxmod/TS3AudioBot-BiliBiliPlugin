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
    private static List<(string bvid, long cid, string title)> recentHistory = new List<(string bvid, long cid, string title)>();


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

    private string GetCookiePath(InvokerData invoker)
    {
        return $"bili_cookie_{invoker.ClientUid}.txt";
    }

    private void SetInvokerCookie(InvokerData invoker, HttpClient client)
    {
        string cookiePath = GetCookiePath(invoker);
        if (File.Exists(cookiePath))
        {
            string cookie = File.ReadAllText(cookiePath);
            if (!string.IsNullOrWhiteSpace(cookie))
            {
                client.DefaultRequestHeaders.Remove("Cookie");
                client.DefaultRequestHeaders.Add("Cookie", cookie);
            }
        }
    }


    [Command("bilibili login")]
    public async Task<string> BilibiliLogin(InvokerData invoker, string cookie)
    {   
        if (string.IsNullOrWhiteSpace(cookie))
        return "用法: !bilibili login [SESSDATA=xxx; bili_jct=xxx; ...]";

        string cookiePath = GetCookiePath(invoker);
        File.WriteAllText(cookiePath, cookie);

        try
        {
            // 使用新的 HttpClient（避免 Header 污染）
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Cookie", cookie);

            string userJson = await client.GetStringAsync("https://api.bilibili.com/x/web-interface/nav");
            JObject userObj = JObject.Parse(userJson);
            string uname = userObj["data"]?["uname"]?.ToString();

            if (!string.IsNullOrEmpty(uname))
                return $"登录成功，账号绑定为：{uname}";

            return "Cookie 已设置，但未能确认登录状态，请检查 Cookie 是否有效。";
        }
        catch (Exception ex)
        {
        return "登录状态确认失败：" + ex.Message;
        }
    }


    [Command("bilibili history")]
    public async Task<string> BilibiliHistory(InvokerData invoker)
    {
        try
        {
            SetInvokerCookie(invoker, http);
            string url = "https://api.bilibili.com/x/web-interface/history/cursor?ps=10&type=archive";
            string json = await http.GetStringAsync(url);
            JObject data = JObject.Parse(json)["data"] as JObject;
            JArray list = data?["list"] as JArray;

            if (list == null || list.Count == 0)
                return "未获取到历史记录，请确认是否已登录。";

            recentHistory.Clear();
            string reply = "最近观看的视频：\n";

            for (int i = 0; i < list.Count; i++)
            {
                JObject item = (JObject)list[i];
                string title = item["title"]?.ToString() ?? "未知标题";
                JObject history = item["history"] as JObject;
                string bvid = history?["bvid"]?.ToString();
                long cid = (long?)history?["cid"] ?? 0;

                if (!string.IsNullOrWhiteSpace(bvid) && cid > 0)
                {
                    recentHistory.Add(new ValueTuple<string, long, string>(bvid, cid, title));
                    reply += $"{i + 1}. {title}\n";
                }
            }

            reply += "\n使用 !bilibili h [编号] 播放对应视频。\n使用 !bilibili addh [编号] 添加到下一播放。";
            return reply;
        }
        catch (Exception ex)
        {
            return "获取历史记录失败：" + ex.Message;
        }
    }

    [Command("bilibili h")]
    public async Task<string> BilibiliHistoryPlay(InvokerData invoker, int index)
    {
        if (recentHistory == null || recentHistory.Count == 0)
            return "历史记录为空，请先使用 !bilibili history 加载最近观看的视频。";

        if (index < 1 || index > recentHistory.Count)
            return $"请输入有效编号（1 - {recentHistory.Count}）。";

        var (bvid, cid, title) = recentHistory[index - 1];
        return await PlayAudio(cid, bvid, invoker);
    }

    [Command("bilibili addh")]
    public async Task<string> BilibiliHistoryAdd(InvokerData invoker, int index)
    {
        if (recentHistory == null || recentHistory.Count == 0)
            return "历史记录为空，请先使用 !bilibili history 加载最近观看的视频。";

        if (index < 1 || index > recentHistory.Count)
            return $"请输入有效编号（1 - {recentHistory.Count}）。";

        var (bvid, cid, title) = recentHistory[index - 1];
        return await EnqueueAudio(cid, bvid, invoker);
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

    [Command("bilibili add")]
    public async Task<string> BilibiliAddCommand(InvokerData invoker, string bvid)
    {
        if (string.IsNullOrWhiteSpace(bvid))
            return "请提供 BV 号，例如：!bilibili add BV1xK4y1a7Yx";

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

                reply += "\n请使用命令 !bilibili addp [编号] 添加对应分P到播放队列。";
                return reply;
            }

            // 无分P情况默认添加第一集
            long cid = (long)viewData["cid"];
            return await EnqueueAudio(cid, bvid, invoker);
        }
        catch (Exception ex)
        {
            return "获取视频信息时出错：" + ex.Message;
        }
    }
    
    [Command("bilibili addp")]
    public async Task<string> BilibiliAddPart(InvokerData invoker, int partIndex)
    {
        if (savedPages == null || savedPages.Count == 0 || string.IsNullOrWhiteSpace(savedBvid))
            return "请先使用 !bilibili add [BV号] 获取视频信息。";

        if (partIndex < 1 || partIndex > savedPages.Count)
            return $"请输入有效编号（1 - {savedPages.Count}）。";

        JObject page = savedPages[partIndex - 1] as JObject;
        long cid = (long)page["cid"];
        return await EnqueueAudio(cid, savedBvid, invoker);
    }

    private async Task<string> EnqueueAudio(long cid, string bvid, InvokerData invoker)
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

            var urlSources = new List<string>();
            if (bestAudio["baseUrl"] != null) urlSources.Add(bestAudio["baseUrl"].ToString());
            if (bestAudio["base_url"] != null) urlSources.Add(bestAudio["base_url"].ToString());

            if (bestAudio["backupUrl"] is JArray backupUrls)
                urlSources.AddRange(backupUrls.Select(u => u.ToString()));
            if (bestAudio["backup_url"] is JArray backupUrls2)
                urlSources.AddRange(backupUrls2.Select(u => u.ToString()));

            foreach (var url in urlSources)
            {
                if (string.IsNullOrWhiteSpace(url)) continue;
                try
                {
                    await _playManager.Enqueue(invoker, url);
                    Console.WriteLine($"已加入队列：{url}");
                    return $"已将 B站 视频 {bvid} 的音频（cid={cid}）添加到播放队列。";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"添加到队列失败：{url}\n原因: {ex.Message}");
                }
            }

            return "所有音频链接加入队列失败。";
        }
        catch (Exception ex)
        {
            return "加入队列失败：" + ex.Message;
        }
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
                    await _playManager.Play(invoker, url);
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