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
	private static List<(string bvid, long cid, string title)> recentHistory =
		new List<(string bvid, long cid, string title)>();
	private static JArray historyPages;
	private static string historyBvid = "";

	public BilibiliPlugin(PlayManager playManager)
	{
		_playManager = playManager;
		http.DefaultRequestHeaders.Remove("Referer");
		http.DefaultRequestHeaders.Add("Referer", "https://www.bilibili.com");
		http.DefaultRequestHeaders.Remove("User-Agent");
		http.DefaultRequestHeaders.Add(
			"User-Agent",
			"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36 Edg/138.0.0.0"
		);
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

	[Command("bilibili qr")]
	public async Task<string> BilibiliQrLogin(InvokerData invoker)
	{
		try
		{
			// 1. 请求生成二维码的key
			string keyUrl = "https://passport.bilibili.com/x/passport-login/web/qrcode/generate";
			string keyResponse = await http.GetStringAsync(keyUrl);
			JObject keyJson = JObject.Parse(keyResponse);

			string qrKey = keyJson["data"]?["qrcode_key"]?.ToString();
			string qrUrl = keyJson["data"]?["url"]?.ToString();

			Console.WriteLine(
				"Key Response: " + keyResponse + "\n qrUrl:" + qrUrl + "\n qrKey:" + qrKey
			);

			if (string.IsNullOrEmpty(qrUrl))
			{
				return "获取二维码失败，请稍后再试，返回的Url为空。";
			}

			qrUrl = qrUrl.Replace(@"\u0026", "&"); // 解决\u0026替换问题

			// 生成二维码
			var qrCodeUrl =
				$"[URL]https://api.2dcode.biz/v1/create-qr-code?data={Uri.EscapeDataString(qrUrl)}[/URL]";

			//轮询
			_ = CheckLoginStatusAsync(qrKey, invoker);
			// 3. 返回二维码图片的URL或其他方式将二维码显示给用户
			// 如果是发送二维码图片到 TS3AudioBot，可以将 qrCodeUrl 提供给用户
			return $"请扫描二维码进行登录： {qrCodeUrl}";
		}
		catch (Exception ex)
		{
			return $"二维码登录失败：{ex.Message}";
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

			string userJson = await client.GetStringAsync(
				"https://api.bilibili.com/x/web-interface/nav"
			);
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
			// 新建HttpClient，避免全局Cookie污染
			var client = new HttpClient();
			string cookiePath = GetCookiePath(invoker);
			if (File.Exists(cookiePath))
			{
				string cookie = File.ReadAllText(cookiePath);
				if (!string.IsNullOrWhiteSpace(cookie))
				{
					client.DefaultRequestHeaders.Add("Cookie", cookie);
				}
			}
			string url = "https://api.bilibili.com/x/web-interface/history/cursor?ps=10&type=archive";
			string json = await client.GetStringAsync(url);
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

		try
		{
			http.DefaultRequestHeaders.Referrer = new Uri("https://www.bilibili.com");
			string viewApi = $"https://api.bilibili.com/x/web-interface/view?bvid={bvid}";
			string viewJson = await http.GetStringAsync(viewApi);
			JObject viewData = JObject.Parse(viewJson)["data"] as JObject;

			if (viewData == null)
				return "无法获取视频详细信息，可能已被删除或不可访问。";

			JArray pages = viewData["pages"] as JArray;
			historyBvid = (string)viewData["bvid"];
			historyPages = pages;

			if (pages != null && pages.Count > 1)
			{
				string reply = $"该视频包含 {pages.Count} 个分P：\n";
				for (int i = 0; i < pages.Count; i++)
				{
					reply += $"{i + 1}. {pages[i]["part"]}\n";
				}

				reply += "\n请使用 !bilibili hp [编号] 播放对应分P。";
				return reply;
			}

			// 若只有一P则直接播放
			long singleCid = (long)viewData["cid"];
			return await PlayAudio(singleCid, bvid, invoker);
		}
		catch (Exception ex)
		{
			return "获取视频分P信息失败：" + ex.Message;
		}
	}

	[Command("bilibili hp")]
	public async Task<string> BilibiliHistoryPlayPart(InvokerData invoker, int partIndex)
	{
		if (
			historyPages == null
			|| historyPages.Count == 0
			|| string.IsNullOrWhiteSpace(historyBvid)
		)
			return "请先使用 !bilibili h [编号] 加载视频信息。";

		if (partIndex < 1 || partIndex > historyPages.Count)
			return $"请输入有效编号（1 - {historyPages.Count}）。";

		JObject page = historyPages[partIndex - 1] as JObject;
		long cid = (long)page["cid"];
		return await PlayAudio(cid, historyBvid, invoker);
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
			return "请提供 BV 号，例如：!bilibili bv BV1UT42167xb";

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
			return "请提供 BV 号，例如：!bilibili add BV1UT42167xb";

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
			string playApi =
				$"https://api.bilibili.com/x/player/playurl?cid={cid}&bvid={bvid}&fnval=16&fourk=1";
			string playJson = await http.GetStringAsync(playApi);
			JArray audioArray = JObject.Parse(playJson)["data"]?["dash"]?["audio"] as JArray;

			if (audioArray == null)
				return "未能获取音频流地址，视频可能不支持 DASH 音频。";

			JObject bestAudio =
				audioArray.OrderByDescending(a => (long)a["bandwidth"]).FirstOrDefault() as JObject;
			if (bestAudio == null)
				return "未能获取有效的音频链接。";

			var urlSources = new List<string>();
			if (bestAudio["baseUrl"] != null)
				urlSources.Add(bestAudio["baseUrl"].ToString());
			if (bestAudio["base_url"] != null)
				urlSources.Add(bestAudio["base_url"].ToString());
			if (bestAudio["backupUrl"] is JArray backupUrls)
				urlSources.AddRange(backupUrls.Select(u => u.ToString()));
			if (bestAudio["backup_url"] is JArray backupUrls2)
				urlSources.AddRange(backupUrls2.Select(u => u.ToString()));

			foreach (var url in urlSources)
			{
				if (string.IsNullOrWhiteSpace(url))
					continue;
				try
				{
					string proxyUrl = $"http://localhost:32181/?{WebUtility.UrlEncode(url)}";
					await _playManager.Enqueue(invoker, proxyUrl);
					Console.WriteLine($"已通过代理加入队列：{proxyUrl}");
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
			string playApi =
				$"https://api.bilibili.com/x/player/playurl?cid={cid}&bvid={bvid}&fnval=16&fourk=1";
			string playJson = await http.GetStringAsync(playApi);
			JArray audioArray = JObject.Parse(playJson)["data"]?["dash"]?["audio"] as JArray;

			if (audioArray == null)
				return "未能获取音频流地址，视频可能不支持 DASH 音频。";

			JObject bestAudio =
				audioArray.OrderByDescending(a => (long)a["bandwidth"]).FirstOrDefault() as JObject;
			if (bestAudio == null)
				return "未能获取有效的音频链接。";

			// 所有候选音频链接
			var urlSources = new List<string>();
			if (bestAudio["baseUrl"] != null)
				urlSources.Add(bestAudio["baseUrl"].ToString());
			if (bestAudio["base_url"] != null)
				urlSources.Add(bestAudio["base_url"].ToString());
			if (bestAudio["backupUrl"] is JArray backupUrls)
				urlSources.AddRange(backupUrls.Select(u => u.ToString()));
			if (bestAudio["backup_url"] is JArray backupUrls2)
				urlSources.AddRange(backupUrls2.Select(u => u.ToString()));

			// 使用本地代理进行播放
			foreach (var url in urlSources)
			{
				if (string.IsNullOrWhiteSpace(url))
					continue;
				try
				{
					string proxyUrl = $"http://localhost:32181/?{WebUtility.UrlEncode(url)}";
					await _playManager.Play(invoker, proxyUrl);
					Console.WriteLine($"播放成功：{proxyUrl}");
					return $"正在通过代理播放 B站 视频 {bvid} 的音频（分P cid={cid}）。";
				}
				catch (Exception ex)
				{
					Console.WriteLine($"播放失败：{url}\n原因: {ex.Message}");
				}
			}

			return "所有音频链接播放失败。";
		}
		catch (Exception ex)
		{
			return "播放失败：" + ex.Message;
		}
	}

	private async Task<string> CheckLoginStatusAsync(string qrKey, InvokerData invoker)
	{
		string checkLoginUrl =
			$"https://passport.bilibili.com/x/passport-login/web/qrcode/poll?qrcode_key={qrKey}";
		string loginStatusResponse;
		bool isLoggedIn = false;
		int time = 0;

		while (isLoggedIn == false)
		{
			loginStatusResponse = await http.GetStringAsync(checkLoginUrl);
			JObject loginStatusJson = JObject.Parse(loginStatusResponse);
			string statusCode = (string)loginStatusJson["data"]?["code"];

			// 打印出登录状态响应
			Console.WriteLine(
				"Login Status Response: " + loginStatusResponse + "statuscode:" + statusCode
			);

			// 登录成功，返回状态码 0
			if (statusCode == "0")
			{
				string fullUrl = (string)loginStatusJson["data"]?["url"];
				isLoggedIn = true;
				string cookie;
				cookie = ExtractCookieFromUrl(fullUrl);
				if (string.IsNullOrWhiteSpace(cookie))
				{
					return "登录成功，但无法获取Cookie信息。";
				}

				// 保存登录后的cookie信息
				string cookiePath = GetCookiePath(invoker);
				File.WriteAllText(cookiePath, cookie);
				return "扫码登录成功！已将登录信息保存。";
			}

			if (statusCode == "86038")
			{
				return "登录失败，二维码已超时";
			}

			if (time >= 30)
			{
				return "登录失败，超时";
			}

			await Task.Delay(2000); // 每2秒检查一次
			time++;
		}
		return "登录失败，请检查二维码是否已扫描并确认登录。";
	}

	private string ExtractCookieFromUrl(string fullUrl)
	{
		// fullUrl 格式：
		// https://passport.biligame.com/crossDomain?DedeUserID=***\u0026DedeUserID__ckMd5=***\u0026Expires=***\u0026SESSDATA=***\u0026bili_jct=***\u0026gourl=https%3A%2F%2Fpassport.bilibili.com

		try
		{
			Uri uri = new Uri(fullUrl);
			var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);

			string sessData = queryParams["SESSDATA"];
			string biliJct = queryParams["bili_jct"];

			// 检查是否提取到了这两个参数，并返回 cookie 字符串
			if (!string.IsNullOrEmpty(sessData) && !string.IsNullOrEmpty(biliJct))
			{
				return $"SESSDATA={sessData};bili_jct={biliJct};";
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error extracting cookie from URL: " + ex.Message);
		}

		return null; // 如果解析失败，返回 null
	}
}
