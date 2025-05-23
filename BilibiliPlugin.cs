using System;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using TS3AudioBot;
using TS3AudioBot.Audio;
using TS3AudioBot.CommandSystem;
using TS3AudioBot.Plugins;

public class BilibiliPlugin : IBotPlugin
{
    private readonly PlayManager _playManager;

    public BilibiliPlugin(PlayManager playManager)
    {
        _playManager = playManager;
    }

    public void Initialize() { }

    long Acid = 0;

    [Command("bilibili")]
    public async Task<string> CommandBilibili(InvokerData invoker, string bvId)
    {
    if (string.IsNullOrWhiteSpace(bvId))
        return "请提供视频的BV号，例如：!bilibili BV1xK4y1a7Yx";

    try
    {
        string viewApi = $"https://api.bilibili.com/x/web-interface/view?bvid={bvId}";
        using var http = new HttpClient();
        string viewJson = await http.GetStringAsync(viewApi);
        var viewData = JObject.Parse(viewJson)["data"];
        if (viewData == null)
            return "未获取到视频信息，请检查BV号是否正确。";

        long cid = (long)viewData["cid"];
        string realBvid = (string)viewData["bvid"];
        Acid = cid;

        string playApi = $"https://api.bilibili.com/x/player/playurl?cid={cid}&bvid={realBvid}&fnval=16&fourk=1";
        string playJson = await http.GetStringAsync(playApi);
        var playData = JObject.Parse(playJson)["data"]?["dash"]?["audio"];
        if (playData == null)
            return "未能获取音频流地址，视频可能不支持DASH音频。";

        var bestAudio = playData.OrderByDescending(a => (long)a["bandwidth"]).FirstOrDefault();
        if (bestAudio == null)
            return "未能获取音频流。";

        // 构建一个音频链接的优先队列
        var urlsToTry = new[]
        {
            (string)bestAudio["baseUrl"],
            (string)bestAudio["base_url"],
            bestAudio["backupUrl"]?.FirstOrDefault()?.ToString(),
            bestAudio["backup_url"]?.FirstOrDefault()?.ToString()
        };

        foreach (var url in urlsToTry)
        {
            if (string.IsNullOrWhiteSpace(url)) continue;
            try
            {
                await _playManager.Enqueue(invoker, url);
                return $"正在播放 B站 视频 [{realBvid}] 的音频。使用链接：{url}";
            }
            catch (Exception playEx)
            {
                // 播放失败，继续尝试下一个 URL
            }
        }

        return $"尝试播放所有音频链接均失败。cid为：[{cid}]";
    }
    catch (Exception ex)
    {
        return $"cid为：[{Acid}]，播放过程中发生错误：" + ex.Message;
    }
    }



    public void Dispose() { }
}
