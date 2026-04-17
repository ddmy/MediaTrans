using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaTrans.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MediaTrans.Services
{
    /// <summary>
    /// 音乐 API 客户端，调用本地 Node.js 音乐搜索服务
    /// </summary>
    public class MusicApiClient
    {
        private readonly string _baseUrl;
        private const int RequestTimeout = 30000; // 30秒

        public MusicApiClient(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
        }

        /// <summary>
        /// 健康检查
        /// </summary>
        public bool CheckHealth()
        {
            try
            {
                string url = _baseUrl + "/api/health";
                string response = HttpGet(url, 5000);
                if (string.IsNullOrEmpty(response)) return false;
                var obj = JObject.Parse(response);
                return obj["status"] != null && obj["status"].ToString() == "ok";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 搜索歌曲
        /// </summary>
        public MusicSearchResponse Search(string keyword, CancellationToken token)
        {
            string url = string.Format("{0}/api/search?keyword={1}&pageSize=30",
                _baseUrl, Uri.EscapeDataString(keyword));

            string response = HttpGet(url, RequestTimeout, token);
            if (string.IsNullOrEmpty(response))
            {
                return new MusicSearchResponse();
            }

            return ParseSearchResponse(response);
        }

        /// <summary>
        /// 获取歌曲播放链接
        /// </summary>
        public MusicStreamInfo GetSongUrl(string platform, string songId, string quality, string songName, string artist)
        {
            string url = string.Format("{0}/api/song/url?platform={1}&id={2}&quality={3}&name={4}&artist={5}",
                _baseUrl,
                Uri.EscapeDataString(platform),
                Uri.EscapeDataString(songId),
                Uri.EscapeDataString(quality ?? "320"),
                Uri.EscapeDataString(songName ?? ""),
                Uri.EscapeDataString(artist ?? ""));

            string response = HttpGet(url, RequestTimeout);
            if (string.IsNullOrEmpty(response)) return null;

            try
            {
                var obj = JObject.Parse(response);
                if (obj["url"] == null || string.IsNullOrEmpty(obj["url"].ToString()))
                {
                    return null;
                }

                var info = new MusicStreamInfo();
                info.Url = obj["url"].ToString();
                info.Quality = obj["quality"] != null ? (int)obj["quality"] : 128;
                info.Format = obj["format"] != null ? obj["format"].ToString() : "mp3";
                info.Size = obj["size"] != null ? (long)obj["size"] : 0;
                return info;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 解析搜索响应
        /// </summary>
        private MusicSearchResponse ParseSearchResponse(string json)
        {
            var result = new MusicSearchResponse();
            result.Results = new List<MusicSearchResult>();
            result.PlatformStatuses = new List<PlatformSearchStatus>();

            try
            {
                var obj = JObject.Parse(json);

                // 解析平台状态
                var platformStatus = obj["platformStatus"] as JObject;
                if (platformStatus != null)
                {
                    foreach (var prop in platformStatus.Properties())
                    {
                        var ps = new PlatformSearchStatus();
                        var val = prop.Value as JObject;
                        if (val == null) continue;
                        ps.Name = val["name"] != null ? val["name"].ToString() : prop.Name;
                        ps.DisplayName = val["displayName"] != null ? val["displayName"].ToString() : prop.Name;
                        ps.Status = val["status"] != null ? val["status"].ToString() : "unknown";
                        ps.Count = val["count"] != null ? (int)val["count"] : 0;
                        ps.Error = val["error"] != null ? val["error"].ToString() : null;
                        result.PlatformStatuses.Add(ps);
                    }
                }

                // 解析合并后的结果
                var merged = obj["merged"] as JArray;
                if (merged != null)
                {
                    foreach (var item in merged)
                    {
                        var sr = new MusicSearchResult();
                        sr.SongName = item["name"] != null ? item["name"].ToString() : "";
                        sr.Artist = item["artist"] != null ? item["artist"].ToString() : "";
                        sr.Album = item["album"] != null ? item["album"].ToString() : "";
                        sr.DurationSeconds = item["duration"] != null ? (int)item["duration"] : 0;
                        sr.DurationText = item["durationText"] != null ? item["durationText"].ToString() : "";

                        // 解析 sources
                        var sources = item["sources"] as JArray;
                        if (sources != null)
                        {
                            foreach (var src in sources)
                            {
                                var ms = new MusicSource();
                                ms.Platform = src["platform"] != null ? src["platform"].ToString() : "";
                                ms.PlatformName = src["platformName"] != null ? src["platformName"].ToString() : "";
                                ms.SongId = src["id"] != null ? src["id"].ToString() : "";
                                ms.NeedVip = src["needVip"] != null && (bool)src["needVip"];
                                ms.DurationSeconds = src["duration"] != null ? (int)src["duration"] : 0;
                                ms.DurationText = src["durationText"] != null ? src["durationText"].ToString() : "";

                                // 解析 quality 数组
                                var qArr = src["quality"] as JArray;
                                if (qArr != null)
                                {
                                    foreach (var q in qArr)
                                    {
                                        ms.Quality.Add(q.ToString());
                                    }
                                }

                                sr.Sources.Add(ms);
                            }
                        }

                        // 默认选中第一个源
                        if (sr.Sources.Count > 0)
                        {
                            sr.SelectedSource = sr.Sources[0];
                        }

                        result.Results.Add(sr);
                    }
                }

                result.TotalCount = obj["totalCount"] != null ? (int)obj["totalCount"] : result.Results.Count;
            }
            catch
            {
                // 解析失败时返回空结果
            }

            return result;
        }

        /// <summary>
        /// 发送 HTTP GET 请求
        /// </summary>
        private string HttpGet(string url, int timeout, CancellationToken token = default(CancellationToken))
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = timeout;
            request.ReadWriteTimeout = timeout;

            // 注册取消
            if (token.CanBeCanceled)
            {
                token.Register(() =>
                {
                    try { request.Abort(); }
                    catch { }
                });
            }

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException("搜索已取消", token);
                }
                System.Diagnostics.Debug.WriteLine(
                    string.Format("[MusicApiClient] HTTP 请求失败: {0}", ex.Message));
                return null;
            }
        }
    }

    /// <summary>
    /// 搜索响应
    /// </summary>
    public class MusicSearchResponse
    {
        public List<MusicSearchResult> Results { get; set; }
        public List<PlatformSearchStatus> PlatformStatuses { get; set; }
        public int TotalCount { get; set; }

        public MusicSearchResponse()
        {
            Results = new List<MusicSearchResult>();
            PlatformStatuses = new List<PlatformSearchStatus>();
        }
    }
}
