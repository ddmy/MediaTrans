'use strict';

/**
 * 酷我音乐 Provider
 * 参考 Listen1 实现: 使用官方 Web API + Token 认证
 *
 * 搜索: GET search.kuwo.cn/r.s
 * 播放链接: GET www.kuwo.cn/api/v1/www/music/playUrl (需 Secret 头)
 */

var BaseProvider = require('./base');
var http = require('../utils/http');
var cryptoUtil = require('../utils/crypto');

/**
 * 酷我加密函数 — 来自酷我网站 (Listen1 提取)
 * 用于生成 Secret 请求头
 */
function kwEncrypt(token, key) {
    if (key == null || key.length <= 0) return null;
    var n = '';
    for (var i = 0; i < key.length; i++) {
        n += key.charCodeAt(i).toString();
    }
    var r = Math.floor(n.length / 5);
    var o = parseInt(
        n.charAt(r) + n.charAt(2 * r) + n.charAt(3 * r) + n.charAt(4 * r) + n.charAt(5 * r)
    );
    var l = Math.ceil(key.length / 2);
    var c = Math.pow(2, 31) - 1;
    if (o < 2) return null;
    var d = Math.round(1e9 * Math.random()) % 1e8;
    for (n += d; n.length > 10; ) {
        n = (parseInt(n.substring(0, 10)) + parseInt(n.substring(10, n.length))).toString();
    }
    n = (o * n + l) % c;
    var f = '';
    for (i = 0; i < token.length; i++) {
        var h = parseInt(token.charCodeAt(i) ^ Math.floor((n / c) * 255));
        f += h < 16 ? '0' + h.toString(16) : h.toString(16);
        n = (o * n + l) % c;
    }
    for (d = d.toString(16); d.length < 8; ) {
        d = '0' + d;
    }
    return f + d;
}

var KW_SECRET_KEY = 'Hm_Iuvt_cdb524f42f23cer9b268564v7y735ewrq2324';

function KuwoProvider() {
    BaseProvider.call(this, 'kuwo', '酷我音乐');
    this._token = null;
    this._tokenTime = 0;
}

KuwoProvider.prototype = Object.create(BaseProvider.prototype);
KuwoProvider.prototype.constructor = KuwoProvider;

/**
 * 解析酷我旧版搜索 API 的非标准 JSON（单引号格式）
 */
KuwoProvider.prototype._parseKuwoJson = function (text) {
    if (typeof text !== 'string') return text;
    text = text.trim();
    if (!text) return null;
    // 替换单引号为双引号
    // 先保护已转义的单引号和值中的特殊情况
    var result = text
        .replace(/\\'/g, '\u0001') // 保护转义的单引号
        .replace(/'/g, '"')         // 替换单引号为双引号
        .replace(/\u0001/g, "'");   // 还原转义的单引号
    try {
        return JSON.parse(result);
    } catch (e) {
        console.log('[kuwo] JSON 解析失败:', e.message);
        return null;
    }
};

/**
 * 搜索歌曲 — 使用 search.kuwo.cn 接口
 */
KuwoProvider.prototype.search = async function (keyword, pageSize) {
    pageSize = pageSize || 30;

    var resp = await http.request({
        url: 'http://search.kuwo.cn/r.s?all=' + encodeURIComponent(keyword) +
            '&ft=music&itemset=web_2013&client=kt&pn=0&rn=' + pageSize +
            '&rformat=json&encoding=utf8',
        method: 'GET',
        referer: 'https://kuwo.cn/',
        timeout: 8000,
        responseType: 'text'
    });

    var results = [];
    if (resp.status === 200 && resp.data) {
        var data = this._parseKuwoJson(resp.data);
        if (data && data.abslist) {
            var songs = data.abslist;
            for (var i = 0; i < songs.length; i++) {
                var song = songs[i];
                var songName = (song.SONGNAME || '').replace(/<[^>]+>/g, '').replace(/&nbsp;/g, ' ').replace(/&amp;/g, '&').replace(/&lt;/g, '<').replace(/&gt;/g, '>').trim();
                var artist = (song.ARTIST || '').replace(/<[^>]+>/g, '').replace(/&nbsp;/g, ' ').replace(/&amp;/g, '&').trim();
                var albumName = (song.ALBUM || '').replace(/<[^>]+>/g, '').replace(/&nbsp;/g, ' ').replace(/&amp;/g, '&').trim();

                // 从 MUSICRID 提取 rid
                var musicRid = song.MUSICRID || song.DC_TARGETID || '';
                var rid = String(musicRid).replace('MUSIC_', '');

                var duration = parseInt(song.DURATION, 10) || 0;

                var qualityList = ['128', '320'];
                if (song.MKVRID || song.NSIG1 === '1') {
                    qualityList.push('flac');
                }

                results.push(this.buildResult({
                    id: rid,
                    name: songName,
                    artist: artist,
                    album: albumName,
                    duration: duration,
                    quality: qualityList,
                    needVip: false
                }));
            }
        }
    }
    return results;
};

/**
 * 获取 Token — 访问 kuwo.cn 获取认证 cookie
 */
KuwoProvider.prototype._getToken = async function () {
    // Token 缓存 10 分钟
    var now = Date.now();
    if (this._token && (now - this._tokenTime) < 10 * 60 * 1000) {
        return this._token;
    }

    try {
        var resp = await http.request({
            url: 'https://www.kuwo.cn/',
            method: 'GET',
            timeout: 8000,
            responseType: 'text',
            retries: 1
        });

        // 从 Set-Cookie 中提取 token
        if (resp.headers && resp.headers['set-cookie']) {
            var cookies = Array.isArray(resp.headers['set-cookie'])
                ? resp.headers['set-cookie']
                : [resp.headers['set-cookie']];
            for (var i = 0; i < cookies.length; i++) {
                var cookie = cookies[i];
                if (cookie.indexOf(KW_SECRET_KEY) >= 0) {
                    var match = new RegExp(KW_SECRET_KEY + '=([^;]+)').exec(cookie);
                    if (match) {
                        this._token = match[1];
                        this._tokenTime = now;
                        console.log('[kuwo] Token 获取成功');
                        return this._token;
                    }
                }
            }
        }

        // 备用：从响应体中提取
        if (resp.data && typeof resp.data === 'string') {
            var bodyMatch = new RegExp(KW_SECRET_KEY + '["\']?\\s*[:=]\\s*["\']?([a-zA-Z0-9_-]+)').exec(resp.data);
            if (bodyMatch) {
                this._token = bodyMatch[1];
                this._tokenTime = now;
                console.log('[kuwo] Token 从页面提取成功');
                return this._token;
            }
        }
    } catch (e) {
        console.log('[kuwo] 获取 Token 失败:', e.message);
    }
    return null;
};

/**
 * 获取歌曲播放链接 — 使用官方 Web API (参考 Listen1)
 */
KuwoProvider.prototype.getSongUrl = async function (songId, quality) {
    var rid = String(songId).replace('MUSIC_', '');

    // 方案1：官方 Web API + Token (Listen1 方案，最可靠)
    var urlResult = await this._tryOfficialApi(rid, quality);
    if (urlResult) return urlResult;

    // 降级音质
    if (quality !== '128') {
        urlResult = await this._tryOfficialApi(rid, '128');
        if (urlResult) return urlResult;
    }

    // 方案2：anti.s 接口 (旧方案，作为备用)
    urlResult = await this._tryAntiServer(rid, quality);
    if (urlResult) return urlResult;

    if (quality !== '128') {
        urlResult = await this._tryAntiServer(rid, '128');
        if (urlResult) return urlResult;
    }

    return null;
};

/**
 * 官方 Web API — 需要 Secret 头 (参考 Listen1)
 */
KuwoProvider.prototype._tryOfficialApi = async function (rid, quality) {
    try {
        var token = await this._getToken();
        var headers = {};

        if (token) {
            var secret = kwEncrypt(token, KW_SECRET_KEY);
            if (secret) {
                headers['Secret'] = secret;
                headers['Cookie'] = KW_SECRET_KEY + '=' + token;
            }
        }

        var resp = await http.request({
            url: 'https://www.kuwo.cn/api/v1/www/music/playUrl?mid=' + rid +
                '&type=music&httpsStatus=1&reqId=&plat=web_www&from=',
            method: 'GET',
            referer: 'https://www.kuwo.cn/',
            headers: headers,
            timeout: 10000
        });

        if (resp.status === 200 && resp.data && resp.data.data && resp.data.data.url) {
            console.log('[kuwo] 官方 API 获取成功');
            return {
                url: resp.data.data.url,
                quality: parseInt(quality) || 128,
                format: quality === 'flac' ? 'flac' : 'mp3',
                size: 0
            };
        } else {
            console.log('[kuwo] 官方 API 无 URL, code:', resp.data && resp.data.code);
            // Token 可能过期，清除缓存
            if (resp.data && resp.data.code === -1) {
                this._token = null;
                this._tokenTime = 0;
            }
        }
    } catch (e) {
        console.log('[kuwo] 官方 API 请求失败:', e.message);
    }
    return null;
};

/**
 * anti.s 接口
 */
KuwoProvider.prototype._tryAntiServer = async function (rid, quality) {
    var formatMap = { '128': 'mp3', '320': 'mp3', 'flac': 'flac' };
    var brMap = { '128': '128kmp3', '320': '320kmp3', 'flac': '2000kflac' };
    var format = formatMap[quality] || 'mp3';
    var br = brMap[quality] || '320kmp3';

    try {
        var resp = await http.request({
            url: 'https://antiserver.kuwo.cn/anti.s?type=convert_url3&rid=' + rid +
                '&format=' + format + '&response=url&br=' + br,
            method: 'GET',
            referer: 'https://kuwo.cn/',
            timeout: 10000,
            responseType: 'text'
        });

        if (resp.status === 200 && resp.data) {
            var urlStr = '';
            var rawData = typeof resp.data === 'string' ? resp.data.trim() : String(resp.data).trim();

            // 新版返回 JSON: {"code": 200, "msg": "success", "url": "https://..."}
            if (rawData.indexOf('{') === 0) {
                try {
                    var jsonData = JSON.parse(rawData);
                    urlStr = jsonData.url || '';
                } catch (e) {
                    urlStr = '';
                }
            } else {
                // 旧版直接返回 URL 字符串
                urlStr = rawData;
            }

            if (urlStr && (urlStr.indexOf('http://') === 0 || urlStr.indexOf('https://') === 0)) {
                return {
                    url: urlStr,
                    quality: parseInt(quality) || 320,
                    format: format,
                    size: 0
                };
            }
        }
    } catch (e) {
        console.log('[kuwo] anti.s 请求失败:', e.message);
    }
    return null;
};

/**
 * 备用: mobi API
 */
KuwoProvider.prototype._tryPlayApi = async function (rid, quality) {
    var brMap = { '128': '128kmp3', '320': '320kmp3', 'flac': '2000kflac' };
    var br = brMap[quality] || '320kmp3';

    try {
        var resp = await http.request({
            url: 'https://mobi.kuwo.cn/mobi.s?f=web&type=convert_url2&rid=' + rid + '&br=' + br,
            method: 'GET',
            referer: 'https://kuwo.cn/',
            timeout: 10000
        });

        if (resp.status === 200 && resp.data) {
            var data = resp.data;
            var urlStr = data.url || '';
            if (urlStr && (urlStr.indexOf('http://') === 0 || urlStr.indexOf('https://') === 0)) {
                return {
                    url: urlStr,
                    quality: parseInt(quality) || 128,
                    format: quality === 'flac' ? 'flac' : 'mp3',
                    size: 0
                };
            }
        }
    } catch (e) {
        console.log('[kuwo] mobi API 请求失败:', e.message);
    }
    return null;
};

module.exports = new KuwoProvider();
