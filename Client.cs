using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using System.Linq;
using Logger;
using System.Net.Http.Headers;
using ExCSS;
using System.Text;

namespace VkClient
{
    public class Client
    {
        public Action<string, Logger.LogData.Type, Exception> Log;
        public Action<Client, string> CaptchaReceived;

        public HttpClient HttpClient;
        public System.Net.CookieContainer Cookies;
        public static int MaxErr = 5;

        public string Login { get; set; }
        public string Password { get; set; }
        public string Referer { get; set; }
        public string RuCaptchaKey { get; set; }
        public string SoftId { get; set; }
        public int RequestTimeout { get; set; }
        public Proxy ProxySettings { get; set; }

        public Client(string login, string password, string userAgent, Proxy p = null)
        {
            Login = login;
            Password = password;
            Referer = "https://m.vk.com/";
            ProxySettings = p;
            Cookies = new System.Net.CookieContainer();
            HttpClientHandler handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = Cookies,
                AllowAutoRedirect = true
            };
            if (ProxySettings != null)
            {
                handler.Proxy = new WebProxy(ProxySettings.Address);
                if (ProxySettings.Login != null && ProxySettings.Password != null)
                {
                    handler.Proxy.Credentials = new NetworkCredential(ProxySettings.Login, ProxySettings.Password);
                }
                handler.UseProxy = true;
            }
            else
            {
                handler.UseProxy = false;
            }
            HttpClient = new HttpClient(handler);
            HttpClient.Timeout = TimeSpan.FromSeconds(30);
            HttpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);

        }

        protected void OnCaptchaReceived(string page)
        {
            CaptchaReceived?.Invoke(this, page);
        }

        private async Task<RequestResult<string>> PerformCaptcha(string url, string page, List<KeyValuePair<string, string>> additionalData = null)
        {
            try
            {
                int count = 0;
                while (true)
                {
                    if (count >= 4)
                    {
                        break;
                    }
                    var checkCaptchaResult = await CheckCaptcha(page, url);
                    if (checkCaptchaResult.Exception == null)
                    {
                        var capcthaResultData = checkCaptchaResult.Data;
                        if (capcthaResultData.Item1 == true)
                        {
                            if (capcthaResultData.Item2?.Item1 != null && capcthaResultData.Item2?.Item2 != null)
                            {
                                HttpRequestMessage msg = new HttpRequestMessage();
                                msg.Headers.Add("Upgrade-Insecure-Requests", 1.ToString());
                                msg.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
                                msg.Headers.Referrer = new Uri(url);
                                msg.Method = HttpMethod.Post;
                                HtmlDocument doc = new HtmlDocument();
                                doc.LoadHtml(page);
                                HtmlNode form = doc.DocumentNode.SelectSingleNode("//form[@method='post']");
                                string action = form.GetAttributeValue("action", "/");
                                string c_url = null;
                                if (!action.ToLower().StartsWith("https"))
                                {
                                    c_url = $"https://m.vk.com{action}";
                                }
                                else
                                {
                                    c_url = action;
                                }
                                msg.RequestUri = new Uri(c_url);
                                if (additionalData != null)
                                {
                                    List<KeyValuePair<string, string>> data = new List<KeyValuePair<string, string>>();
                                    data.AddRange(additionalData);
                                    data.Add(new KeyValuePair<string, string>("captcha_sid", capcthaResultData.Item2.Item1));
                                    data.Add(new KeyValuePair<string, string>("captcha_key", capcthaResultData.Item2.Item2));
                                    msg.Content = new FormUrlEncodedContent(data.ToArray());
                                }
                                else
                                {
                                    msg.Content = new FormUrlEncodedContent(new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("captcha_sid", capcthaResultData.Item2.Item1), new KeyValuePair<string, string>("captcha_key", capcthaResultData.Item2.Item2) });
                                }
                                page = await (await HttpClient.SendAsync(msg)).Content.ReadAsStringAsync();
                                Thread.Sleep(RequestTimeout);
                                count++;
                            }
                            else
                            {
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        return new RequestResult<string>(null) { Exception = checkCaptchaResult.Exception };
                    }
                }
                return new RequestResult<string>(page);
            }
            catch (Exception ex)
            {
                return new RequestResult<string>(null) { Exception = ex };
            }
        }

        private async Task<RequestResult<string>> SendGet(string url, string referer = null)
        {
            try
            {
                HttpRequestMessage msg = new HttpRequestMessage();
                msg.Headers.Add("Referer", referer ?? "https://m.vk.com/");
                msg.Headers.Add("Upgrade-Insecure-Requests", 1.ToString());
                msg.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
                msg.RequestUri = new Uri(url);
                msg.Method = System.Net.Http.HttpMethod.Get;
                var sa = await HttpClient.SendAsync(msg);
                string str = await sa.Content.ReadAsStringAsync();
                Thread.Sleep(RequestTimeout);
                var pcaptcha = await PerformCaptcha(url, str);
                return new RequestResult<string>(pcaptcha.Data) { Exception = pcaptcha.Exception };
            }
            catch (Exception ex)
            {
                return new RequestResult<string>(null) { Exception = ex };
            }
        }

        private async Task<byte[]> GetCaptchaImage(string url, string referer)
        {
            HttpRequestMessage msg = new HttpRequestMessage();
            msg.Headers.Add("Referer", referer);
            msg.Headers.Add("Upgrade-Insecure-Requests", 1.ToString());
            msg.Headers.Add("Accept", "image/webp,image/*,*/*;q=0.8");
            msg.RequestUri = new Uri(url);
            msg.Method = System.Net.Http.HttpMethod.Get;
            var rmsg = await HttpClient.SendAsync(msg);
            var data = await rmsg.Content.ReadAsByteArrayAsync();
            Thread.Sleep(RequestTimeout);
            File.WriteAllBytes("c.bmp", data);
            return data;
        }

        private Task<RequestResult<string>> GetMainPage()
        {
            return SendGet("https://m.vk.com");
        }

        public async Task<RequestResult<bool>> SetRuLang()
        {
            try
            {
                RequestResult<string> main = await SendPost("https://m.vk.com/settings?act=select_lang", "https://m.vk.com/login", "act=select_lang&_nlm=1&_tstat=settings%2C0%2C0%2C217%2C10&_ref=login");
                if (main.Exception == null)
                {
                    Regex findHash = new Regex("hash=(\\w+)");
                    Match hashMatch = findHash.Match(main.Data);
                    if (hashMatch.Success)
                    {
                        await SendPost($"https://m.vk.com/settings?act=change_regional&hash={hashMatch.Groups[1].Value}&lang_id=0", "https://m.vk.com/settings?act=select_lang", $"act=change_regional&hash={hashMatch.Groups[1].Value}&lang_id=0&_nlm=1&_tstat=login%2C0%2C0%2C235%2C7&_ref=settings");
                        return new RequestResult<bool>(true);
                    }
                    else
                    {
                        return new RequestResult<bool>(false);
                    }
                }
                else
                {
                    return new RequestResult<bool>(false) { Exception = main.Exception };
                }
            }
            catch (Exception ex)
            {
                return new RequestResult<bool>(false) { Exception = ex };
            }
        }

        private StreamContent CreateFileContent(Stream stream, string fileName, string contentType)
        {
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = "\"files\"",
                FileName = "\"" + fileName + "\""
            };
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            return fileContent;
        }

        private async Task<string> UploadCaptchaFile(byte[] captcha)
        {
            string url = string.Format("http://rucaptcha.com/in.php");

            string fileName = "vk_captcha_image.png";

            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");

            MultipartFormDataContent multiContent = new MultipartFormDataContent(boundary);
            multiContent.Headers.Remove("Content-Type");
            multiContent.Headers.TryAddWithoutValidation("Content-Type", $"multipart/form-data; boundary={boundary}");

            MemoryStream fileStream = new MemoryStream();
            fileStream.Write(captcha, 0, captcha.Length);
            fileStream.Position = 0;

            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = "\"file\"",
                FileName = "\"" + fileName + "\""
            };
            multiContent.Add(fileContent);
            multiContent.Add(new StringContent(SoftId), "soft_id");
            multiContent.Add(new StringContent(RuCaptchaKey), "key");
            var response = await HttpClient.PostAsync(url, multiContent);
            var responseText = await response.Content.ReadAsStringAsync();
            return responseText;
        }

        private async Task<RequestResult<string>> SendForm(string url, string referer, params KeyValuePair<string, string>[] parameters)
        {
            try
            {
                HttpRequestMessage msg = new HttpRequestMessage();
                msg.Headers.Add("Upgrade-Insecure-Requests", 1.ToString());
                msg.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
                if (referer != null)
                {
                    msg.Headers.Add("Referer", referer);
                }
                else
                {
                    msg.Headers.Add("Referer", "https://m.vk.com/");
                }
                msg.RequestUri = new Uri(url);
                msg.Method = System.Net.Http.HttpMethod.Post;
                if (parameters != null)
                {
                    msg.Content = new FormUrlEncodedContent(parameters);
                    string s = await msg.Content.ReadAsStringAsync();
                }
                var sa = await HttpClient.SendAsync(msg);
                string str = await sa.Content.ReadAsStringAsync();
                var pcaptcha = await PerformCaptcha(url, str);
                return new RequestResult<string>(pcaptcha.Data) { Exception = pcaptcha.Exception };
            }
            catch (Exception ex)
            {
                return new RequestResult<string>(null) { Exception = ex };
            }
        }

        private async Task<RequestResult<string>> SendPost(string url, string referer = null, string str_content = null, KeyValuePair<string, string>[] additionalheaders = null)
        {
            try
            {
                HttpRequestMessage msg = new HttpRequestMessage();
                msg.Headers.Add("Upgrade-Insecure-Requests", 1.ToString());
                msg.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
                if (additionalheaders != null)
                {
                    foreach (var v in additionalheaders)
                    {
                        msg.Headers.Add(v.Key, v.Value);
                    }
                }
                if (referer != null)
                {
                    msg.Headers.Add("Referer", referer);
                }
                else
                {
                    msg.Headers.Add("Referer", "https://m.vk.com/");
                }
                msg.RequestUri = new Uri(url);
                msg.Method = System.Net.Http.HttpMethod.Post;
                if (str_content != null)
                {
                    msg.Content = new StringContent(str_content);
                }
                var sa = await HttpClient.SendAsync(msg);
                string str = await (sa).Content.ReadAsStringAsync();
                var pcaptcha = await PerformCaptcha(url, str);
                return new RequestResult<string>(pcaptcha.Data) { Exception = pcaptcha.Exception };
            }
            catch (Exception ex)
            {
                return new RequestResult<string>(null) { Exception = ex };
            }
        }

        private Task<RequestResult<string>> GetFeedPage()
        {
            return SendGet("https://m.vk.com/feed");
        }

        protected void SendLog(string msg, Logger.LogData.Type type, Exception ex = null)
        {
            if (Log != null)
            {
                Log(msg, type, ex);
            }
        }

        private async Task<RequestResult<Tuple<bool, Tuple<string, string>>>> CheckCaptcha(string received_text, string url)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(RuCaptchaKey))
                {
                    Regex rgx = new Regex("captcha\\.php\\?sid=(\\w+)");

                    Match m = rgx.Match(received_text);

                    if (!m.Success)
                    {
                        rgx = new Regex("captcha[.]php[?]s=0[&]sid=(\\w+)");
                        m = rgx.Match(received_text);
                    }

                    if (m.Success)
                    {
                        try
                        {
                            OnCaptchaReceived(received_text);
                        }
                        catch (Exception ex)
                        {
                            Log(ex.Message, LogData.Type.Error, ex);
                        }

                        SendLog($"Обнаружена каптча", LogData.Type.Warning);

                        var recognized = await SendCaptcha(m.Groups[1].Value, url);

                        if (recognized.Data == null)
                        {
                            recognized = await SendCaptcha(m.Groups[1].Value, url);
                        }

                        if (recognized.Data == null)
                        {
                            recognized = await SendCaptcha(m.Groups[1].Value, url);
                        }

                        if (recognized.Data != null)
                        {
                            SendLog($"Решение каптчи завершено успешно", LogData.Type.Warning);
                        }
                        else
                        {
                            SendLog($"Не удалось решить каптчу", LogData.Type.Error);
                        }

                        return new RequestResult<Tuple<bool, Tuple<string, string>>>(new Tuple<bool, Tuple<string, string>>(true, new Tuple<string, string>(m.Groups[1].Value, recognized.Data))) { Exception = recognized.Exception };
                    }

                    return new RequestResult<Tuple<bool, Tuple<string, string>>>(new Tuple<bool, Tuple<string, string>>(false, null));
                }

                return new RequestResult<Tuple<bool, Tuple<string, string>>>(new Tuple<bool, Tuple<string, string>>(false, null));
            }
            catch (Exception ex)
            {
                return new RequestResult<Tuple<bool, Tuple<string, string>>>(null) { Exception = ex };
            }
        }

        public async Task<RequestResult<string>> SendCaptcha(string captcha_id, string url)
        {
            try
            {
                byte[] img = await GetCaptchaImage($"https://m.vk.com/captcha.php?sid={captcha_id}&s=1", url);
                string response = await UploadCaptchaFile(img);
                Regex okRgx = new Regex("OK\\|(\\d+)");
                Match idM = okRgx.Match(response);
                if (idM.Success)
                {
                    while (true)
                    {
                        Thread.Sleep(1000);
                        RequestResult<string> result =
                            await SendGet(
                                $"http://rucaptcha.com/res.php?key={RuCaptchaKey}&action=get&id={idM.Groups[1].Value}");
                        if (result.Exception == null)
                        {
                            if (result.Data == "CAPCHA_NOT_READY")
                            {
                                continue;
                            }
                            else
                            {
                                Regex resultRgx = new Regex("OK\\|(\\w+)");
                                Match resultM = resultRgx.Match(result.Data);
                                if (resultM.Success)
                                {
                                    return new RequestResult<string>(resultM.Groups[1].Value);
                                }
                                else
                                {
                                    return new RequestResult<string>(null);
                                }
                            }
                        }
                        else
                        {
                            return new RequestResult<string>(null) { Exception = result.Exception };
                        }
                    }
                }
                else
                {
                    return new RequestResult<string>(null);
                }
            }
            catch (Exception ex)
            {
                SendLog($"Не удалось отправить каптчу на проверку {url}", LogData.Type.Error, ex);
                return new RequestResult<string>(null) { Exception = ex };
            }
        }

        public async Task<RequestResult<string>> GetHomePage()
        {
            try
            {
                RequestResult<string> feed = await GetFeedPage();
                if (feed.Exception == null)
                {
                    Regex pageRgx = new Regex("<li id=\"l_pr\" class=\"\"><a href=\"/(\\w+)\"");
                    Match pageM = pageRgx.Match(feed.Data);
                    if (pageM.Success)
                    {
                        return new RequestResult<string>($"https://m.vk.com/{pageM.Groups[1].Value}");
                    }
                    else
                    {
                        return new RequestResult<string>(null);
                    }
                }
                else
                {
                    return new RequestResult<string>(null) { Exception = feed.Exception };
                }
            }
            catch (Exception ex)
            {
                return new RequestResult<string>(null) { Exception = ex };
            }
        }

        public async Task<RequestResult<bool>> IsAuthorized()
        {
            var mainPage = await SendGet("https://m.vk.com/feed");
            if (mainPage.Exception == null)
            {
                if (mainPage.Data.Contains("upanel bl_cont"))
                {
                    return new RequestResult<bool>(true);
                }
                else
                {
                    return new RequestResult<bool>(false);
                }
            }
            else
            {
                return new RequestResult<bool>(false) { Exception = mainPage.Exception };
            }
        }

        public async Task<RequestResult<bool>> Logout()
        {
            try
            {
                var vk = await GetFeedPage();
                if (vk.Exception == null)
                {
                    Regex logoutHref = new Regex(@"https://login.m.vk.com/\?act=logout&hash=(\w+)&reason=tn");
                    Match href = logoutHref.Match(vk.Data);
                    if (href.Success)
                    {
                        await SendGet(href.Value);
                        return await IsAuthorized();
                    }
                    else
                    {
                        return await IsAuthorized();
                    }
                }
                else
                {
                    return new RequestResult<bool>(false) { Exception = vk.Exception };
                }
            }
            catch (Exception ex)
            {
                SendLog($"Ошибка при выполнениит деавторизации", LogData.Type.Error, ex);
                return new RequestResult<bool>(false) { Exception = ex };
            }
        }

        public async Task<RequestResult<bool>> Repost(string post_url, string Message = "")
        {
            try
            {
                var page = await SendGet(post_url);
                if (page.Exception == null)
                {
                    if (!string.IsNullOrWhiteSpace(page.Data))
                    {
                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(page.Data);
                        HtmlNode share = doc.DocumentNode.SelectSingleNode("//a[@class='item_share _i']");
                        if (share != null)
                        {
                            string href = share.GetAttributeValue("href", null);
                            if (!string.IsNullOrWhiteSpace(href))
                            {
                                string url = $"https://m.vk.com{href}";
                                var get = await SendGet(url);
                                if (get.Exception == null)
                                {
                                    if (!get.Data.Contains("item_share item_sel _i"))
                                    {
                                        if (!string.IsNullOrWhiteSpace(get.Data))
                                        {
                                            doc = new HtmlDocument();
                                            doc.LoadHtml(get.Data);
                                            HtmlNode form =
                                                doc.DocumentNode.SelectSingleNode("//form[@id='publish_add_form']");
                                            if (form != null)
                                            {
                                                string confirm_url =
                                                    $"https://m.vk.com{form.GetAttributeValue("action", "/")}";
                                                List<KeyValuePair<string, string>> data =
                                                    new List<KeyValuePair<string, string>>();
                                                var param = doc.DocumentNode.SelectNodes("//input[@type='hidden']");
                                                foreach (var v in param)
                                                {
                                                    string key = v.GetAttributeValue("name", null);
                                                    if (key != null)
                                                    {
                                                        string val = v.GetAttributeValue("value", "");
                                                        data.Add(new KeyValuePair<string, string>(key, val));
                                                    }
                                                }
                                                var result = await SendForm(confirm_url, url, data.ToArray());
                                                if (result.Exception == null)
                                                {
                                                    return new RequestResult<bool>(result.Data.Contains("service_msg service_msg_ok"));
                                                }
                                                else
                                                {
                                                    return new RequestResult<bool>(false) { Exception = result.Exception };
                                                }
                                            }
                                            else
                                            {
                                                return new RequestResult<bool>(false);
                                            }
                                        }
                                        else
                                        {
                                            return new RequestResult<bool>(false);
                                        }
                                    }
                                    else
                                    {
                                        return new RequestResult<bool>(false);
                                    }
                                }
                                else
                                {
                                    return new RequestResult<bool>(false) { Exception = get.Exception };
                                }
                            }
                            else
                            {
                                return new RequestResult<bool>(false);
                            }
                        }
                        else
                        {
                            return new RequestResult<bool>(false);
                        }
                    }
                    else
                    {
                        return new RequestResult<bool>(false);
                    }
                }
                else
                {
                    return new RequestResult<bool>(false) { Exception = page.Exception };
                }
            }
            catch (Exception ex)
            {
                SendLog($"Ошибка при попытке выполнить репост {post_url}", LogData.Type.Error, ex);
                return new RequestResult<bool>(false) { Exception = ex };
            }
        }

        public async Task<RequestResult<List<string>>> GetFriendsList(string id)
        {
            try
            {
                List<string> friends = new List<string>();
                int offset = 0;
                while (true)
                {
                    var page = await SendGet($"https://m.vk.com/friends?id={id}?offset={offset}");
                    if (page.Exception == null)
                    {
                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(page.Data);
                        var list = doc.DocumentNode.SelectNodes("//a[@class='si_owner']");
                        if (list == null)
                        {
                            break;
                        }
                        if (list.Count == 0)
                        {
                            break;
                        }
                        foreach (var href in list)
                        {
                            friends.Add($"https://m.vk.com{href.GetAttributeValue("href", "")}");
                        }
                        offset = friends.Count;
                    }
                    else
                    {
                        return new RequestResult<List<string>>(null) { Exception = page.Exception };
                    }
                }
                return new RequestResult<List<string>>(friends);
            }
            catch (Exception ex)
            {
                return new RequestResult<List<string>>(null) { Exception = ex };
            }
        }

        public async Task<RequestResult<List<Human>>> GetFriends(string id)
        {
            try
            {
                List<Human> friends = new List<Human>();
                int offset = 0;
                while (true)
                {
                    var page = await SendGet($"https://m.vk.com/friends?id={id}?offset={offset}");
                    if (page.Exception == null)
                    {
                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(page.Data);
                        var list = doc.DocumentNode.SelectNodes("//a[@class='si_owner']");
                        if (list == null)
                        {
                            break;
                        }
                        if (list.Count == 0)
                        {
                            break;
                        }
                        foreach (var href in list)
                        {
                            string url = $"https://m.vk.com{href.GetAttributeValue("href", "")}";
                            Human h = (await GetHumanData(url))?.Data;
                            if (h != null)
                            {
                                friends.Add(h);
                            }
                        }
                        offset = friends.Count;
                    }
                    else
                    {
                        return new RequestResult<List<Human>>(null) { Exception = page.Exception };
                    }
                }
                return new RequestResult<List<Human>>(friends);
            }
            catch (Exception ex)
            {
                return new RequestResult<List<Human>>(null) { Exception = ex };
            }
        }

        public async Task<InviteResult> InviteToGroup(string groupUrl, Human frientData)
        {
            try
            {
                var invitePage = await SendGet($"{groupUrl}?act=invite&q={HttpUtility.UrlEncode(frientData.Name)}");
                if (invitePage.Exception == null)
                {
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(invitePage.Data);
                    var invites = doc.DocumentNode.SelectNodes("//a[@class='inline_item']");
                    if (invites?.Count > 0)
                    {
                        string inviteUrl = null;
                        foreach (var i in invites)
                        {
                            var href = i.GetAttributeValue("href", null);
                            if (href != null)
                            {
                                if (href.Contains(frientData.Id))
                                {
                                    inviteUrl = $"https://m.vk.com{href}";
                                    break;
                                }
                            }
                        }
                        if (inviteUrl != null)
                        {
                            var resultPage = await SendGet(inviteUrl);
                            if (resultPage.Exception == null)
                            {
                                return new InviteResult(resultPage.Data.Contains("service_msg service_msg_ok"),
                                    resultPage.Data.Contains("service_msg service_msg_warning")
                                    && resultPage.Data.Contains("Вы можете пригласить только"),
                                    resultPage.Data.Contains("service_msg service_msg_warning")
                                    && !resultPage.Data.Contains("Вы можете пригласить только"), resultPage.Data);
                            }
                            else
                            {
                                return new InviteResult(false, false, false, resultPage.Data) { Exception = resultPage.Exception };
                            }
                        }
                        else
                        {
                            return new InviteResult(false, false, false, invitePage.Data);
                        }
                    }
                    else
                    {
                        return new InviteResult(false, false, false, invitePage.Data);
                    }
                }
                else
                {
                    return new InviteResult(false, false, false, invitePage.Data) { Exception = invitePage.Exception };
                }
            }
            catch (Exception ex)
            {
                return new InviteResult(false, false, false, null) { Exception = ex };
            }
        }

        public async Task<AddFriendResult> AddFriend(string url, string loadedPage = null)
        {
            try
            {
                var page = loadedPage;
                if (page == null)
                {
                    var get = await SendGet(url);
                    if (get.Exception != null)
                    {
                        return new AddFriendResult(false, false) { Exception = get.Exception };
                    }
                    page = get.Data;
                }
                if (!string.IsNullOrWhiteSpace(page))
                {
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(page);
                    var op = doc.DocumentNode.SelectSingleNode("//div[@class='op_block']");
                    if (op != null)
                    {
                        var a = op.SelectNodes(".//a");
                        foreach (var n in a)
                        {
                            string href = n.GetAttributeValue("href", null);
                            if (href != null)
                            {
                                if (href.Contains("/friends?act=accept&id"))
                                {
                                    var result = await SendGet($"https://m.vk.com{href}");
                                    if (result.Exception == null)
                                    {
                                        return new AddFriendResult(result.Data.Contains("service_msg service_msg_ok"),
                                            result.Data.Contains("service_msg service_msg_warning")
                                            && result.Data.Contains("Вы не можете добавлять больше друзей за один день"));
                                    }
                                    else
                                    {
                                        return new AddFriendResult(false, false) { Exception = result.Exception };
                                    }
                                }
                            }
                        }
                        return new AddFriendResult(false, false);
                    }
                    else
                    {
                        return new AddFriendResult(false, false);
                    }
                }
                else
                {
                    return new AddFriendResult(false, false);
                }
            }
            catch (Exception ex)
            {
                SendLog($"Ошибка при попытке отправить запрос в друзья {url}", LogData.Type.Error, ex);
                return new AddFriendResult(false, false) { Exception = ex };
            }
        }

        public async Task<RequestResult<Human>> GetHumanData(string url)
        {
            try
            {
                var infoPage = await SendGet($"{url}?act=info");
                if (infoPage.Exception == null)
                {
                    if (infoPage.Data != null)
                    {
                        Human human = new Human();
                        human.PageURL = url;
                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(infoPage.Data);
                        List<HtmlNode> humanData = new List<HtmlNode>();
                        HtmlNodeCollection temp = doc.DocumentNode.SelectNodes(@"//dl[@class='pinfo_row _pinfo']");
                        if (temp != null)
                        {
                            foreach (var n in temp)
                            {
                                humanData.Add(n);
                            }
                        }
                        temp = doc.DocumentNode.SelectNodes("//dl[@class='pinfo_row']");
                        if (temp != null)
                        {
                            foreach (var n in temp)
                            {
                                humanData.Add(n);
                            }
                        }
                        HtmlNode name = doc.DocumentNode.SelectSingleNode("//h2[@class='op_header']");
                        if (!string.IsNullOrWhiteSpace(name?.InnerText))
                        {
                            var page = await SendGet($"{url}");
                            if (page.Exception == null)
                            {
                                human.Name = name.InnerText;
                                Regex idRgx = new Regex("like[?]act=add[&]object[=]wall(\\d+)");
                                Match idMatch = idRgx.Match(page.Data);

                                if (!idMatch.Success)
                                {
                                    var pf = doc.DocumentNode.SelectSingleNode("//ul[@class='profile_menu']");
                                    if (pf != null)
                                    {
                                        string inner = pf.InnerHtml.ToLower();
                                        Regex pfRgx = new Regex("albums(\\d+)");
                                        idMatch = pfRgx.Match(inner);
                                    }
                                }

                                if (idMatch.Success)
                                {
                                    human.Id = idMatch.Groups[1].Value;
                                }
                                else
                                {
                                    return new RequestResult<Human>(null);
                                }
                                if (humanData?.Count > 0)
                                {
                                    foreach (HtmlNode node in humanData)
                                    {
                                        HtmlNode paramName = node.SelectSingleNode("./dt");
                                        HtmlNode value = node.SelectSingleNode("./dd");
                                        if (paramName != null && value != null)
                                        {
                                            string pnameNorm = paramName.InnerText.Replace(":", "");
                                            if (!human.Data.ContainsKey(pnameNorm))
                                            {
                                                human.Data.Add(pnameNorm, value.InnerText);
                                            }
                                        }
                                    }
                                }

                                var activityBlock = doc.DocumentNode.SelectSingleNode("//div[@class='pp_online']");

                                if (activityBlock == null)
                                    activityBlock = doc.DocumentNode.SelectSingleNode("//div[@class='pp_last_activity']");

                                string date = null;
                                string activityBlockText = null;
                                Match dateMatch = null;

                                if (activityBlock != null)
                                {
                                    try
                                    {
                                        activityBlockText = activityBlock.InnerText;
                                        if (string.IsNullOrWhiteSpace(activityBlockText))
                                        {
                                            human.LastActivity = DateTime.MinValue;
                                        }
                                        else if (activityBlockText.Contains("сегодня"))
                                        {
                                            human.LastActivity = DateTime.Now.Date;
                                        }
                                        else if (activityBlockText.Contains("вчера"))
                                        {
                                            human.LastActivity = DateTime.Now.Date.AddDays(-1).Date;
                                        }
                                        else if (activityBlockText.Contains("назад"))
                                        {
                                            human.LastActivity = DateTime.Now.Date;
                                        }
                                        else if (activityBlockText.Contains("Online"))
                                        {
                                            human.LastActivity = DateTime.Now.Date;
                                        }
                                        else
                                        {
                                            Regex dateRgx = new Regex(@"заходил(а{0,1}) ((\d{1,2}) (\w+))");
                                            dateMatch = dateRgx.Match(activityBlockText);
                                            if (dateMatch.Success)
                                            {
                                                date = dateMatch.Groups[2].Value;
                                                int year = DateTime.Now.Year;
                                                date = date.Replace(" января", $".01.{year}");
                                                date = date.Replace(" февраля", $".02.{year}");
                                                date = date.Replace(" марта", $".03.{year}");
                                                date = date.Replace(" апреля", $".04.{year}");
                                                date = date.Replace(" мая", $".05.{year}");
                                                date = date.Replace(" июня", $".06.{year}");
                                                date = date.Replace(" июля", $".07.{year}");
                                                date = date.Replace(" августа", $".08.{year}");
                                                date = date.Replace(" сентября", $".09.{year}");
                                                date = date.Replace(" октября", $".10.{year}");
                                                date = date.Replace(" ноября", $".11.{year}");
                                                date = date.Replace(" декабря", $".12.{year}");
                                                DateTime dt;
                                                if (DateTime.TryParse(date, out dt))
                                                {
                                                    human.LastActivity = dt;
                                                }
                                                else
                                                {
                                                    human.LastActivity = DateTime.MinValue;
                                                }
                                            }
                                            else
                                            {
                                                human.LastActivity = DateTime.MinValue;
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        human.LastActivity = DateTime.MinValue;
                                    }
                                }
                                return new RequestResult<Human>(human);
                            }
                            else
                            {
                                return new RequestResult<Human>(null) { Exception = page.Exception };
                            }
                        }
                        else
                        {
                            return new RequestResult<Human>(null);
                        }
                    }
                    else
                    {
                        return new RequestResult<Human>(null);
                    }
                }
                else
                {
                    return new RequestResult<Human>(null) { Exception = infoPage.Exception };
                }
            }
            catch (Exception ex)
            {
                SendLog($"Ошибка при загрузке данных пользователя {url}", LogData.Type.Error, ex);
                return new RequestResult<Human>(null) { Exception = ex };
            }
        }

        public async Task<RequestResult<List<string>>> GetHumansUrl(string url, int offset, int count)
        {
            try
            {
                if (url.Contains("&offset="))
                {
                    Regex offsetRgx = new Regex("&offset=(\\d+)");
                    Match offsetM = offsetRgx.Match(url);
                    if (offsetM.Success)
                    {
                        url = url.Replace(offsetM.Value, "");
                    }
                }
                List<string> humans = new List<string>();
                int lastCount = -1;
                int err = 0;
                while (count > 0)
                {
                    var page = await SendPost($"{url}&offset={offset}");
                    if (page.Exception == null)
                    {
                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(page.Data);
                        {
                            HtmlNodeCollection hcol = doc.DocumentNode.SelectNodes("//div[@class='ii_body']");
                            if (hcol?.Count > 0)
                            {
                                foreach (HtmlNode h in hcol)
                                {
                                    if (count <= 0)
                                    {
                                        break;
                                    }
                                    HtmlNode link = h?.ParentNode;
                                    if (link != null)
                                    {
                                        string href = link.GetAttributeValue("href", "");
                                        if (!string.IsNullOrWhiteSpace(href))
                                        {
                                            string hUrl = $"https://m.vk.com{href}";
                                            humans.Add(hUrl);
                                        }
                                    }
                                }
                            }
                        }
                        {
                            HtmlNodeCollection hcol = doc.DocumentNode.SelectNodes("//div[@class='si_body']");
                            if (hcol?.Count > 0)
                            {
                                foreach (HtmlNode h in hcol)
                                {
                                    if (count <= 0)
                                    {
                                        break;
                                    }
                                    HtmlNode link = h?.ParentNode;
                                    if (link != null)
                                    {
                                        string href = link.GetAttributeValue("href", "");
                                        if (!string.IsNullOrWhiteSpace(href))
                                        {
                                            string hUrl = $"https://m.vk.com{href}";
                                            humans.Add(hUrl);
                                        }
                                    }
                                }
                            }
                        }
                        if (lastCount == count)
                        {
                            if (err >= MaxErr)
                            {
                                break;
                            }
                            else
                            {
                                err++;
                            }
                        }
                        else
                        {
                            lastCount = count;
                        }
                    }
                    else
                    {
                        return new RequestResult<List<string>>(null) { Exception = page.Exception };
                    }
                }
                return new RequestResult<List<string>>(humans);
            }
            catch (Exception ex)
            {
                SendLog($"Ошибка при загрузке данных пользователей по адресу {url}", LogData.Type.Error, ex);
                return new RequestResult<List<string>>(null) { Exception = ex };
            }
        }

        public async Task<RequestResult<List<Human>>> GetHumans(string url, int offset, int count)
        {
            try
            {
                if (url.Contains("offset"))
                {
                    Regex offsetRgx = new Regex("&offset=(\\d+)");
                    Match offsetM = offsetRgx.Match(url);
                    if (offsetM.Success)
                    {
                        url = url.Replace(offsetM.Value, "");
                    }
                }
                List<Human> humans = new List<Human>();
                int lastCount = -1;
                int err = 0;
                while (count > 0)
                {
                    var page = await SendPost($"{url}&offset={offset}");
                    if (page.Exception == null)
                    {
                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(page.Data);
                        {
                            HtmlNodeCollection hcol = doc.DocumentNode.SelectNodes("//div[@class='ii_body']");
                            if (hcol?.Count > 0)
                            {
                                foreach (HtmlNode h in hcol)
                                {
                                    if (count <= 0)
                                    {
                                        break;
                                    }
                                    await SendPost($"{url}&offset={offset}");
                                    HtmlNode link = h?.ParentNode;
                                    if (link != null)
                                    {
                                        string href = link.GetAttributeValue("href", "");
                                        if (!string.IsNullOrWhiteSpace(href))
                                        {
                                            string hUrl = $"https://m.vk.com{href}";
                                            Human humanData = (await GetHumanData(hUrl))?.Data;
                                            if (humanData != null)
                                            {
                                                humans.Add(humanData);
                                                count--;
                                                offset++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        {
                            HtmlNodeCollection hcol = doc.DocumentNode.SelectNodes("//div[@class='si_body']");
                            if (hcol?.Count > 0)
                            {
                                foreach (HtmlNode h in hcol)
                                {
                                    if (count <= 0)
                                    {
                                        break;
                                    }
                                    await SendPost($"{url}&offset={offset}");
                                    HtmlNode link = h?.ParentNode;
                                    if (link != null)
                                    {
                                        string href = link.GetAttributeValue("href", "");
                                        if (!string.IsNullOrWhiteSpace(href))
                                        {
                                            string hUrl = $"https://m.vk.com{href}";
                                            Human humanData = (await GetHumanData(hUrl))?.Data;
                                            if (humanData != null)
                                            {
                                                humans.Add(humanData);
                                                count--;
                                                offset++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (lastCount == count)
                        {
                            if (err >= MaxErr)
                            {
                                break;
                            }
                            else
                            {
                                err++;
                            }
                        }
                        else
                        {
                            lastCount = count;
                        }
                    }
                    else
                    {
                        return new RequestResult<List<Human>>(null) { Exception = page.Exception };
                    }
                }
                return new RequestResult<List<Human>>(humans);
            }
            catch (Exception ex)
            {
                SendLog($"Ошибка при загрузке данных пользователей по адресу {url}", LogData.Type.Error, ex);
                return new RequestResult<List<Human>>(null) { Exception = ex };
            }
        }

        public async Task<RequestResult<List<string>>> GetFriendsIDByFilter(string userID, string ageFrom, string ageTo, string cityID, string sex)
        {
            try
            {
                List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>();
                parameters.Add(new KeyValuePair<string, string>("act", "filter_friends"));
                parameters.Add(new KeyValuePair<string, string>("uid", userID));
                parameters.Add(new KeyValuePair<string, string>("al", "1"));
                parameters.Add(new KeyValuePair<string, string>("al_ad", "0"));
                if (ageFrom != null)
                    parameters.Add(new KeyValuePair<string, string>("age_from", ageFrom));
                if (ageTo != null)
                    parameters.Add(new KeyValuePair<string, string>("age_to", ageTo));
                if (cityID != null)
                    parameters.Add(new KeyValuePair<string, string>("city", cityID));
                if (sex != null)
                    parameters.Add(new KeyValuePair<string, string>("sex", sex));
                var result = await SendForm("https://vk.com/friends", "https://vk.com/friends", parameters.ToArray());
                if (result.Exception == null)
                {
                    List<string> idlist = new List<string>();
                    int index = result.Data.IndexOf("<!json>") + 7;
                    var r = result.Data.Substring(index);
                    dynamic data = JArray.Parse(r);
                    foreach (dynamic item in data)
                    {
                        idlist.Add(item.ToString());
                    }
                    return new RequestResult<List<string>>(idlist);
                }
                else
                {
                    return new RequestResult<List<string>>(null) { Exception = result.Exception };
                }
            }
            catch (Exception ex)
            {
                return new RequestResult<List<string>>(null) { Exception = ex };
            }
        }

        public async Task<AuthResult> Auth()
        {
            try
            {
                var t = await SetRuLang();
                if (t.Exception != null)
                {
                    return new AuthResult(false, false) { Exception = t.Exception };
                }
                t = await IsAuthorized();
                if (t.Exception != null)
                {
                    return new AuthResult(false, false) { Exception = t.Exception };
                }
                if (t.Data)
                {
                    t = await Logout();
                }
                if (t.Exception != null)
                {
                    return new AuthResult(false, false) { Exception = t.Exception };
                }
                var vk = await SendGet("https://m.vk.com/login");
                if (vk.Exception != null)
                {
                    return new AuthResult(false, false) { Exception = vk.Exception };
                }
                Regex lgH = new Regex("lg_h=(\\w+)");
                Regex ipH = new Regex("ip_h=(\\w+)");
                Match lgM = lgH.Match(vk.Data);
                Match ipM = ipH.Match(vk.Data);
                if (lgM.Success && ipM.Success)
                {
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(vk.Data);
                    HtmlNode urlNode = doc.DocumentNode.SelectSingleNode("//form");
                    string url = urlNode.GetAttributeValue("action", null);
                    if (url != null)
                    {
                        var authResult = await SendForm(url, "https://m.vk.com/login",
                            new KeyValuePair<string, string>("email", Login),
                            new KeyValuePair<string, string>("pass", Password));
                        if (authResult.Exception == null)
                        {
                            if (authResult.Data.Contains("owner_panel index_panel"))
                            {
                                return new AuthResult(true, authResult.Data.Contains("text_panel login_blocked_panel"));
                            }
                            else
                            {
                                return new AuthResult(false, authResult.Data.Contains("text_panel login_blocked_panel"));
                            }
                        }
                        else
                        {
                            return new AuthResult(false, false) { Exception = authResult.Exception };
                        }
                    }
                    else
                    {
                        return new AuthResult(false, false);
                    }
                }
                else
                {
                    return new AuthResult(false, false);
                }
            }
            catch (Exception ex)
            {
                SendLog($"Ошибка при авторизации {Login}/{Password}", LogData.Type.Error, ex);
                return new AuthResult(false, false) { Exception = ex };
            }
        }

        public async Task<RequestResult<string>> GetGroupID(string url)
        {
            try
            {
                var page = await SendGet(url);
                if (page.Exception == null)
                {
                    Regex rgx = new Regex("class=\"dropdown__item\" href=\"/club(\\d+)[?]act=leave");
                    Match m = rgx.Match(page.Data);
                    if (m.Success)
                    {
                        return new RequestResult<string>(m.Groups[1].Value);
                    }
                    else
                    {
                        return new RequestResult<string>(null);
                    }
                }
                else
                {
                    return new RequestResult<string>(null) { Exception = page.Exception };
                }
            }
            catch (Exception ex)
            {
                return new RequestResult<string>(null) { Exception = ex };
            }
        }

        public async Task<RequestResult<List<string>>> GetAvailableInvitesForGroup(string groupUrl, string userID)
        {
            try
            {
                List<string> idList = new List<string>();
                var groupID = await GetGroupID(groupUrl);
                if (groupID.Exception != null)
                {
                    return new RequestResult<List<string>>(null) { Exception = groupID.Exception };
                }
                if (groupID.Data != null)
                {
                    var result = await SendForm("https://vk.com/al_friends.php", $"https://vk.com/friends?act=invite&group_id={groupID}", new KeyValuePair<string, string>[] {
                    new KeyValuePair<string, string>("act", "load_friends_silent"),
                    new KeyValuePair<string, string>("al", "1"),
                    new KeyValuePair<string, string>("gid", groupID.Data),
                    new KeyValuePair<string, string>("id", userID)
                });
                    if (result.Exception != null)
                    {
                        return new RequestResult<List<string>>(null) { Exception = result.Exception };
                    }
                    var r = result.Data.Substring(result.Data.IndexOf("<!>0<!>{") + 7);
                    Regex idRgx = new Regex(@"\['(\d+)',");
                    MatchCollection idM = idRgx.Matches(r);
                    if (idM.Count > 0)
                    {
                        foreach (Match m in idM)
                        {
                            idList.Add(m.Groups[1].Value);
                        }
                    }
                }
                return new RequestResult<List<string>>(idList);
            }
            catch (Exception ex)
            {
                return new RequestResult<List<string>>(null) { Exception = ex };
            }
        }

        public async Task<RequestResult<bool>> LikePost(string post_url)
        {
            try
            {
                var page = await SendGet(post_url);
                if (page.Exception != null)
                {
                    return new RequestResult<bool>(false) { Exception = page.Exception };
                }
                if (!string.IsNullOrWhiteSpace(page.Data))
                {
                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(page.Data);
                    HtmlNode like = doc.DocumentNode.SelectSingleNode("//a[@class='item_like _i']");
                    if (like != null)
                    {
                        string href = like.GetAttributeValue("href", null);
                        if (!string.IsNullOrWhiteSpace(href))
                        {
                            await SendGet($"https://m.vk.com{href}");
                            return new RequestResult<bool>(true);
                        }
                        else
                        {
                            return new RequestResult<bool>(false);
                        }
                    }
                    else
                    {
                        return new RequestResult<bool>(false);
                    }
                }
                else
                {
                    return new RequestResult<bool>(false);
                }
            }
            catch (Exception ex)
            {
                SendLog($"Ошибка при попытке добавить лайк к посту {post_url}", LogData.Type.Error, ex);
                return new RequestResult<bool>(false) { Exception = ex };
            }
        }

        public async Task<RequestResult<List<Post>>> GetWallPosts(string url, int offset, int count)
        {
            try
            {
                List<Post> posts = new List<Post>();

                int lastCount = -1;

                int err = 0;

                while (count > 0)
                {
                    string arg = $"{url}?offset={offset}&own=1#posts";

                    var get = await SendGet(arg);

                    if (get.Exception != null)
                    {
                        return new RequestResult<List<Post>>(null) { Exception = get.Exception };
                    }

                    string posts_page = get.Data;

                    HtmlDocument doc = new HtmlDocument();

                    doc.LoadHtml(posts_page);

                    HtmlNodeCollection postsCollection = doc.DocumentNode.SelectNodes("//div[@class='wall_item']");

                    if (postsCollection != null)
                    {
                        foreach (HtmlNode post in postsCollection)
                        {
                            Post p = new Post();

                            HtmlNode text = post.SelectSingleNode(".//div[@class='pi_text']");
                            HtmlNode time = post.SelectSingleNode(".//a[@class='wi_date']");
                            HtmlNode pUrl = post.SelectSingleNode(".//a[@class='wi_date']");
                            HtmlNode author = post.SelectSingleNode(".//a[@class='pi_author']");
                            HtmlNode likes = post.SelectSingleNode(".//b[@class='v_like']");
                            HtmlNode reposts = post.SelectSingleNode(".//b[@class='v_share']");
                            HtmlNode explain = post.SelectSingleNode(".//span[@class='explain']");
                            HtmlNodeCollection imagesNodes = post.SelectNodes(".//div[@class='thumbs_map fill']//a");

                            if (explain != null)
                            {
                                continue;
                            }

                            HtmlNodeCollection all = post.ChildNodes;

                            if (text != null)
                            {
                                p.Text = text.InnerText;
                            }

                            if (time != null)
                            {
                                p.Time = time.InnerText;
                            }

                            if (pUrl != null)
                            {
                                string href = pUrl.GetAttributeValue("href", null);
                                if (!string.IsNullOrWhiteSpace(href))
                                {
                                    p.Url = $"https://m.vk.com{href}";
                                }
                            }

                            if (author != null)
                            {
                                Author pa = new Author(author.InnerText,
                                    $"https://m.vk.com{author.GetAttributeValue("href", string.Empty)}");
                                p.Author = pa;
                            }

                            if (likes != null)
                            {
                                if (!string.IsNullOrWhiteSpace(likes.InnerText))
                                {
                                    p.LikesCount = Convert.ToInt32(likes.InnerText.Replace(" ", ""));
                                }
                            }

                            if (reposts != null)
                            {
                                if (!string.IsNullOrWhiteSpace(reposts.InnerText))
                                {
                                    p.RepostsCount = Convert.ToInt32(reposts.InnerText.Replace(" ", ""));
                                }
                            }

                            if (imagesNodes?.Count > 0)
                            {
                                foreach (var n in imagesNodes)
                                {
                                    EmbeddedImage pi = new EmbeddedImage();

                                    var divImgNode = n.SelectSingleNode(".//div");

                                    if (divImgNode != null)
                                    {
                                        string style = divImgNode.GetAttributeValue("style", null);

                                        if (style != null)
                                        {
                                            style = $"style_body {{{style}}}";

                                            Parser cssParser = new Parser();

                                            var sheet = cssParser.Parse(style);

                                            var imgUrl = sheet.StyleRules
                                                            .SelectMany(r => r.Declarations)
                                                            .FirstOrDefault(d => d.Name.Equals("background-image", StringComparison.InvariantCultureIgnoreCase))
                                                            .Term
                                                            .ToString();

                                            if (imgUrl != null)
                                            {
                                                imgUrl = imgUrl.Replace("url(", "").Replace(")", "");

                                                pi.SourceUrl = imgUrl;
                                            }
                                        }
                                    }

                                    string href = n.GetAttributeValue("href", null);

                                    if (href != null)
                                    {
                                        href = $"https://m.vk.com{href}";

                                        pi.Url = href;

                                        Regex idrgx = new Regex("photo((\\d+)_(\\d+))[?]");

                                        Match idm = idrgx.Match(href);

                                        if (idm.Success)
                                        {
                                            pi.SourceID = idm.Groups[1].Value;
                                        }
                                    }

                                    p.Images.Add(pi);
                                }
                            }

                            posts.Add(p);
                        }

                        count -= postsCollection.Count;
                        offset += postsCollection.Count;
                    }

                    if (lastCount == count)
                    {
                        if (err >= MaxErr)
                        {
                            break;
                        }
                        else
                        {
                            err++;
                        }
                    }

                    lastCount = count;

                    Thread.Sleep(100);
                }
                return new RequestResult<List<Post>>(posts);
            }
            catch (Exception ex)
            {
                SendLog($"Ошибка при загрузке постов со страницы {url}", LogData.Type.Error, ex);
                return new RequestResult<List<Post>>(null) { Exception = ex };
            }
        }

        public async Task<RequestResult<byte[]>> GetImage(string url)
        {
            try
            {
                HttpRequestMessage msg = new HttpRequestMessage();
                msg.Headers.Add("Referer", "https://m.vk.com/");
                msg.Headers.Add("Upgrade-Insecure-Requests", 1.ToString());
                msg.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
                msg.RequestUri = new Uri(url);
                msg.Method = System.Net.Http.HttpMethod.Get;
                var sa = await HttpClient.SendAsync(msg);
                var data = await sa.Content.ReadAsByteArrayAsync();
                return new RequestResult<byte[]>(data);
            }
            catch (Exception ex)
            {
                return new RequestResult<byte[]>(null) { Exception = ex };
            }
        }

        public async Task<RequestResult<bool>> SendPost(Post p, string target)
        {
            try
            {
                var id = await GetCurrentUserID();

                if (id.Exception != null)
                    return new RequestResult<bool>(false) { Exception = id.Exception };

                if (string.IsNullOrWhiteSpace(id.Data))
                    return new RequestResult<bool>(false);

                var page = await SendGet(target);

                if (page.Exception != null)
                    return new RequestResult<bool>(false) { Exception = page.Exception };

                if (page.Data == null)
                    return new RequestResult<bool>(false);

                HtmlDocument pageDocument = new HtmlDocument();
                pageDocument.LoadHtml(page.Data);

                var clearAttachments = pageDocument.DocumentNode.SelectSingleNode("//div[@class='cp_option _ib']//a");

                if (clearAttachments != null)
                {

                    var clearAttachmentsUrl = clearAttachments.GetAttributeValue("href", null);

                    if (clearAttachmentsUrl == null)
                        return new RequestResult<bool>(false);

                    clearAttachmentsUrl = $"https://m.vk.com{clearAttachmentsUrl}";

                    page = await SendGet(clearAttachmentsUrl);

                    if (page.Exception != null)
                        return new RequestResult<bool>(false) { Exception = page.Exception };

                    if (page.Data == null)
                        return new RequestResult<bool>(false);

                    pageDocument.LoadHtml(page.Data);

                    var clearForm = pageDocument.DocumentNode.SelectSingleNode("//div[@class='form_item']//form");

                    if (clearForm == null)
                        return new RequestResult<bool>(false);

                    var clearActionUrl = clearForm.GetAttributeValue("action", null);

                    if (clearActionUrl == null)
                        return new RequestResult<bool>(false);

                    clearActionUrl = $"https://m.vk.com{clearActionUrl}";

                    page = await SendGet(clearActionUrl);

                    if (page.Exception != null)
                        return new RequestResult<bool>(false) { Exception = page.Exception };

                    if (page.Data == null)
                        return new RequestResult<bool>(false);

                    pageDocument.LoadHtml(page.Data);
                }

                if (p.Images.Count > 0)
                {
                    foreach (var pi in p.Images)
                    {
                        string addAttachmentsUrl = $"https://m.vk.com/attachments?target=wall{id.Data}&from=profile";

                        page = await SendGet(addAttachmentsUrl);

                        if (page.Exception != null)
                            return new RequestResult<bool>(false) { Exception = page.Exception };

                        if (page.Data == null)
                            return new RequestResult<bool>(false);

                        pageDocument.LoadHtml(page.Data);

                        var img = await GetImage(pi.SourceUrl);

                        HtmlDocument doc = new HtmlDocument();
                        doc.LoadHtml(page.Data);

                        var formNode = doc.DocumentNode.SelectSingleNode("//div[@class='form_item upload_form']//form");

                        if (formNode != null)
                        {
                            var actionUrl = formNode.GetAttributeValue("action", null);

                            if (actionUrl != null)
                            {
                                try
                                {
                                    MemoryStream ms = new MemoryStream();
                                    ms.Write(img.Data, 0, img.Data.Length);
                                    ms.Position = 0;

                                    MultipartFormDataContent multipartContent = new MultipartFormDataContent("------WebKitFormBoundarycvn9AyxSwdoHrwi2");
                                    multipartContent.Headers.Remove("Content-Type");
                                    multipartContent.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=------WebKitFormBoundarycvn9AyxSwdoHrwi2");
                                    multipartContent.Add(new StreamContent(ms), "\"photo\"", $"img{img.Data.Length}.jpeg");

                                    var postImg = await HttpClient.PostAsync(actionUrl, multipartContent);

                                    var response = await postImg.Content.ReadAsStringAsync();
                                }
                                catch (Exception ex)
                                {
                                    continue;
                                }
                            }
                        }

                        page = await SendGet(target);
                    }
                }

                page = await SendGet(target);

                if (page.Exception != null)
                    return new RequestResult<bool>(false) { Exception = page.Exception };

                if (page.Data == null)
                    return new RequestResult<bool>(false);

                pageDocument.LoadHtml(page.Data);

                var attachmentNodes = pageDocument.DocumentNode.SelectNodes("//div[@class='medias_thumb thumb_item']//input[2]");

                var sendPostForm = pageDocument.DocumentNode.SelectSingleNode("//div[@class='create_post create_post_extra']//form");

                if (sendPostForm == null)
                    return new RequestResult<bool>(false);

                string actionValue = sendPostForm.GetAttributeValue("action", null);

                if (actionValue == null)
                    return new RequestResult<bool>(false);

                string postUrl = $"https://m.vk.com{actionValue}";

                string hash = null;

                Regex hashRgx = new Regex("&hash=((\\d|\\w)+)");

                Match hashM = hashRgx.Match(actionValue);

                if (hashM.Success)
                    hash = hashM.Groups[1].Value;

                List<KeyValuePair<string, string>> postData = new List<KeyValuePair<string, string>>();

                postData.Add(new KeyValuePair<string, string>("act", "post"));
                postData.Add(new KeyValuePair<string, string>("from", "profile"));
                postData.Add(new KeyValuePair<string, string>("hash", hash));
                postData.Add(new KeyValuePair<string, string>("message", p.Text));

                //act=post&from=profile&hash=cc484fadbe7f0a2fd3&message=new&attach1_type=photo&attach1=422669554_456239029&_ref=id422669554

                if (attachmentNodes != null)
                {
                    int a_id = 1;

                    foreach (var at in attachmentNodes)
                    {
                        postData.Add(new KeyValuePair<string, string>($"attach{ a_id }_type", "photo"));
                        postData.Add(new KeyValuePair<string, string>($"attach{a_id}", at.Attributes[2].Value));

                        a_id++;
                    }
                }

                postData.Add(new KeyValuePair<string, string>("_ref", $"id{id.Data}"));

                var post = await SendForm(postUrl, "https://m.vk.com", postData.ToArray());

                return new RequestResult<bool>(true);
            }
            catch (Exception ex)
            {
                return new RequestResult<bool>(false) { Exception = ex };
            }
        }

        public async Task<RequestResult<string>> GetCurrentUserID()
        {
            try
            {
                var feed = await SendGet("https://m.vk.com/settings");
                if (feed.Exception != null)
                {
                    return new RequestResult<string>(null) { Exception = feed.Exception };
                }
                Regex rgx = new Regex("src=\"https://r3.mail.ru/k[?]vk_id=(\\d+)&src=mobile&data=\"");
                Match m = rgx.Match(feed.Data);
                if (m.Success)
                {
                    return new RequestResult<string>(m.Groups[1].Value);
                }
                else
                {
                    return new RequestResult<string>(null);
                }
            }
            catch (Exception ex)
            {
                return new RequestResult<string>(null) { Exception = ex };
            }
        }

        public async Task<RequestResult<Dialog>> GetDialog(string dialogUrl, int msgPerDialog)
        {
            try
            {
                Dialog dialogData = new Dialog();

                int msgOffset = 0;

                var dialogPageResult = await SendGet(dialogUrl);

                if (dialogPageResult.Data == null)
                    return new RequestResult<Dialog>(null);

                HtmlDocument dialogDoc = new HtmlDocument();

                dialogDoc.LoadHtml(dialogPageResult.Data);

                var miAuthor = dialogDoc.DocumentNode.SelectSingleNode(".//span[@class='sub_header_label']")?.InnerText ?? "UNKNOWN";

                dialogData.Name = miAuthor;
                dialogData.Url = dialogUrl;

                var messages = dialogDoc.DocumentNode.SelectNodes("//div[@class='mi_iwrap']");

                if (messages == null)
                    return new RequestResult<Dialog>(null);

                if (messages.Count < 1)
                    return new RequestResult<Dialog>(null);

                for (int i = 0; i < messages.Count; i++)
                    messages[i] = messages[i].ParentNode;

                Dictionary<string, Author> authors = new Dictionary<string, Author>();

                while (true)
                {
                    int dMsgCount = 0;

                    foreach (var m in messages)
                    {
                        Author author = null;

                        var aNode = m.SelectSingleNode(".//div[@class='mi_iwrap']//a");

                        if (aNode == null)
                            continue;

                        var aHref = aNode.GetAttributeValue("href", null);

                        if (aHref == null)
                            continue;

                        aHref = $"https://m.vk.com{aHref}";

                        if (authors.ContainsKey(aHref))
                        {
                            author = authors[aHref];
                        }
                        else
                        {
                            var human = await GetHumanData(aHref);

                            if (human.Exception != null)
                                continue;

                            if (human.Data != null)
                            {
                                author = new Author(human.Data.Name, aHref);
                            }
                            else continue;

                            if (author == null)
                                continue;

                            Regex idRgx = new Regex("id(\\d+)");

                            Match idMatch = idRgx.Match(aHref);

                            if (idMatch.Success)
                            {
                                author.ID = idMatch.Groups[1].Value;
                            }

                            authors.Add(aHref, author);
                        }

                        if (author == null)
                            continue;

                        Message messageData = new Message();
                        messageData.Author = author;
                        messageData.Dialog = dialogData;

                        var timeNode = m.SelectSingleNode(".//div[@class='mi_cont']//div[@class='mi_head']//a[1]");

                        if (timeNode == null)
                            continue;

                        try
                        {
                            string rt = timeNode.InnerText;

                            Regex dayRgx = new Regex("(\\d+) ");
                            Match dayM = dayRgx.Match(rt);

                            DateTime dt = new DateTime();

                            if (dayM.Success)
                            {
                                string dayStr = dayM.Groups[1].Value;

                                int day = int.Parse(dayStr);
                                int year = DateTime.Now.Year;
                                int month = 1;

                                if (rt.Contains("янв"))
                                    month = 1;

                                if (rt.Contains("фев"))
                                    month = 2;

                                if (rt.Contains("мар"))
                                    month = 3;

                                if (rt.Contains("апр"))
                                    month = 4;

                                if (rt.Contains("мая"))
                                    month = 5;

                                if (rt.Contains("июн"))
                                    month = 6;

                                if (rt.Contains("июл"))
                                    month = 7;

                                if (rt.Contains("авг"))
                                    month = 8;

                                if (rt.Contains("сен"))
                                    month = 9;

                                if (rt.Contains("окт"))
                                    month = 10;

                                if (rt.Contains("ноя"))
                                    month = 11;

                                if (rt.Contains("дек"))
                                    month = 12;

                                dt = new DateTime(year, month, day);
                            }
                            else
                            {
                                Regex timeRgx = new Regex("(\\d+):(\\d+)");
                                Match timeM = timeRgx.Match(rt);

                                if (timeM.Success)
                                {
                                    dt = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
                                    var ts = TimeSpan.Parse(rt);

                                    if (ts > DateTime.Now.TimeOfDay)
                                        dt = dt.AddDays(-1);

                                    dt = dt.Add(ts);
                                }
                                else if (rt == "вчера")
                                {
                                    dt = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day).AddDays(-1);
                                }
                                else if (rt == "сегодня")
                                {
                                    dt = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
                                }
                            }

                            messageData.ReceiptTime = dt;
                        }
                        catch { }

                        var msgNode = m.SelectSingleNode(".//div[@class='mi_cont']//div[@class='mi_body']//div[@class='mi_text']");

                        if (msgNode != null)
                        {
                            messageData.Text = msgNode.InnerText;
                        }

                        //stickers
                        {
                            var imgNodes = m.SelectNodes(".//a[@class='medias_thumb']//img");

                            if (imgNodes != null)
                            {
                                if (imgNodes.Count > 0)
                                {
                                    foreach (var imgNode in imgNodes)
                                    {
                                        string src = imgNode.GetAttributeValue("src", null);

                                        if (src != null)
                                        {
                                            src = $"https://m.vk.com{src}";

                                            var img = await GetImage(src);

                                            if (img.Exception != null)
                                                continue;

                                            if (img.Data == null)
                                                continue;

                                            MemoryStream ms = new MemoryStream();

                                            ms.Write(img.Data, 0, img.Data.Length);
                                            ms.Position = 0;

                                            Bitmap bitmap = new Bitmap(ms);

                                            EmbeddedImage ei = new EmbeddedImage();
                                            ei.Image = bitmap;
                                            ei.SourceUrl = src;

                                            messageData.Images.Add(ei);
                                        }
                                    }
                                }
                            }
                        }

                        //photo
                        {
                            var imgNodes = m.SelectNodes(".//a[@class='thumb_map thumb_map_wide thumb_map_l al_photo']//div");

                            if (imgNodes != null)
                            {
                                foreach (var imn in imgNodes)
                                {
                                    try
                                    {
                                        string style = imn.GetAttributeValue("style", null);

                                        if (style == null)
                                            continue;

                                        ExCSS.Parser prs = new Parser();
                                        var styleSheet = prs.Parse($"test{{{style}}}");

                                        var url = ((dynamic)styleSheet.StyleRules[0].Declarations[0].Term).Value;

                                        if (url != null)
                                        {
                                            var img = await GetImage(url);

                                            if (img.Exception != null)
                                                continue;

                                            if (img.Data == null)
                                                continue;

                                            MemoryStream ms = new MemoryStream();

                                            ms.Write(img.Data, 0, img.Data.Length);
                                            ms.Position = 0;

                                            Bitmap bitmap = new Bitmap(ms);

                                            EmbeddedImage ei = new EmbeddedImage();
                                            ei.Image = bitmap;
                                            ei.SourceUrl = url;

                                            messageData.Images.Add(ei);
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }

                        dialogData.Messages.Add(messageData);

                        dMsgCount++;

                        if (dMsgCount >= msgPerDialog)
                            break;
                    }

                    msgOffset += dMsgCount;

                    if (dMsgCount >= msgPerDialog)
                        break;

                    if (dialogData.Messages.Count >= msgPerDialog)
                        break;

                    if (dMsgCount < 20)
                        break;

                    var href = $"{dialogUrl}&offset={msgOffset}";

                    dialogPageResult = await SendGet(href);

                    if (dialogPageResult.Data == null)
                        continue;

                    dialogDoc = new HtmlDocument();

                    dialogDoc.LoadHtml(dialogPageResult.Data);

                    messages = dialogDoc.DocumentNode.SelectNodes("//div[@class='mi_iwrap']");

                    if (messages == null)
                        continue;

                    if (messages.Count < 1)
                        continue;

                    for (int i = 0; i < messages.Count; i++)
                        messages[i] = messages[i].ParentNode;

                    if (dialogData.Messages.Count >= msgPerDialog)
                        break;
                }

                return new RequestResult<Dialog>(dialogData);
            }
            catch (Exception ex)
            {
                return new RequestResult<Dialog>(null) { Exception = ex };
            }
        }

        public async Task<RequestResult<List<Dialog>>> GetMessages(int dialogsCount, int msgPerDialog, HashSet<string> dialogsBlackList = null)
        {
            try
            {
                List<Dialog> dialogs = new List<Dialog>();

                var id = await GetCurrentUserID();

                if (id.Exception != null)
                    return new RequestResult<List<Dialog>>(null) { Exception = id.Exception };

                if (string.IsNullOrWhiteSpace(id.Data))
                    return new RequestResult<List<Dialog>>(null);

                int offset = 0;

                while (true)
                {
                    var mailPageResult = await SendGet($"https://m.vk.com/mail?&offset={offset}", "https://m.vk.com");

                    if (mailPageResult.Exception != null)
                        return new RequestResult<List<Dialog>>(null) { Exception = mailPageResult.Exception };

                    string mailPage = mailPageResult.Data;

                    HtmlDocument mailPageDoc = new HtmlDocument();

                    mailPageDoc.LoadHtml(mailPage);

                    var dialogNodes = mailPageDoc.DocumentNode.SelectNodes("//div[@class='di_cont']");

                    if (dialogNodes == null)
                        break;

                    if (dialogNodes.Count == 0)
                        break;

                    foreach (var dialog in dialogNodes)
                    {
                        if (dialogs.Count >= dialogsCount)
                            break;

                        var parent = dialog.ParentNode;

                        var href = parent?.GetAttributeValue("href", null);

                        string dialogHref = $"https://m.vk.com{href}";

                        if (dialogsBlackList?.Contains(dialogHref) == true)
                            continue;

                        var d = await GetDialog(dialogHref, msgPerDialog);

                        if (d.Data != null)
                            dialogs.Add(d.Data);
                    }

                    offset = dialogs.Count;

                    if (dialogs.Count == 0)
                        break;

                    if (dialogs.Count >= dialogsCount)
                        break;
                }

                return new RequestResult<List<Dialog>>(dialogs);
            }
            catch (Exception ex)
            {
                return new RequestResult<List<Dialog>>(null) { Exception = ex };
            }
        }

        public async Task<RequestResult<bool>> SendMessage(string userUrl, string message)
        {
            try
            {
                var page = await SendGet(userUrl);

                if (page.Exception != null)
                    return new RequestResult<bool>(false) { Exception = page.Exception, Page = page.Data };

                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(page.Data);

                var sendMsgButton = htmlDoc.DocumentNode.SelectSingleNode("//a[@class='button wide_button']");

                if (sendMsgButton == null)
                    return new RequestResult<bool>(false) { Info = "SendMessageButtonNotFound", Page = page.Data };

                var msgHref = sendMsgButton.GetAttributeValue("href", null);

                if (msgHref == null)
                    return new RequestResult<bool>(false) { Info = "SendMessageButtonHrefNotFound", Page = page.Data };

                msgHref = $"https://m.vk.com{msgHref}";

                page = await SendGet(msgHref);

                if (page.Exception != null)
                    return new RequestResult<bool>(false) { Exception = page.Exception, Page = page.Data };

                htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(page.Data);

                var sendForm = htmlDoc.DocumentNode.SelectSingleNode("//form[@id='write_form']");

                if (sendForm == null)
                    return new RequestResult<bool>(false) { Info = "FormNotFound", Page = page.Data };

                var actionUrl = sendForm.GetAttributeValue("action", null);

                if (actionUrl == null)
                    return new RequestResult<bool>(false) { Info = "ActionURLNotFound", Page = page.Data };

                actionUrl = $"https://m.vk.com{actionUrl}";

                page = await SendForm(actionUrl, msgHref,
                    new KeyValuePair<string, string>("message", message));

                if (page.Exception != null)
                    return new RequestResult<bool>(false) { Exception = page.Exception, Page = page.Data };

                htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(page.Data);

                var msgOkElement = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='service_msg service_msg_ok']");

                return new RequestResult<bool>(msgOkElement != null) { Info = "", Page = page.Data };
            }
            catch (Exception ex)
            {
                return new RequestResult<bool>(false) { Exception = ex };
            }
        }
    }
}
