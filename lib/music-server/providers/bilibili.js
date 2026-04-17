'use strict';

/**
 * Bilibili 音频 Provider (参考 Listen1)
 *
 * 搜索: GET api.bilibili.com/x/web-interface/search/type (视频搜索)
 * 播放链接:
 *   - 音频区: GET bilibili.com/audio/music-service-c/web/url?sid=
 *   - 视频区: GET api.bilibili.com/x/player/playurl?fnval=16 (DASH 音频流)
 */

var BaseProvider = require('./base');
var http = require('../utils/http');

/**
 * 构建查询字符串
 */
function buildQuery(params) {
    var parts = [];
    var keys = Object.keys(params);
    for (var i = 0; i < keys.length; i++) {
        parts.push(encodeURIComponent(keys[i]) + '=' + encodeURIComponent(params[keys[i]]));
    }
    return parts.join('&');
}

function BilibiliProvider() {
    BaseProvider.call(this, 'bilibili', 'B站');
}

BilibiliProvider.prototype = Object.create(BaseProvider.prototype);
BilibiliProvider.prototype.constructor = BilibiliProvider;

/**
 * HTML 实体解码
 */
BilibiliProvider.prototype._htmlDecode = function (str) {
    if (!str) return '';
    return str
        .replace(/&amp;/g, '&')
        .replace(/&lt;/g, '<')
        .replace(/&gt;/g, '>')
        .replace(/&quot;/g, '"')
        .replace(/&#39;/g, "'")
        .replace(/<[^>]+>/g, '');
};

/**
 * 搜索歌曲 — 搜索 B 站视频
 */
BilibiliProvider.prototype.search = async function (keyword, pageSize) {
    pageSize = pageSize || 30;

    try {
        var qs = buildQuery({
            '__refresh__': 'true',
            '_extra': '',
            'context': '',
            'page': '1',
            'page_size': String(Math.min(pageSize, 42)),
            'platform': 'pc',
            'highlight': '1',
            'single_column': '0',
            'keyword': keyword,
            'category_id': '',
            'search_type': 'video',
            'dynamic_offset': '0',
            'preload': 'true',
            'com2co': 'true'
        });

        var resp = await http.request({
            url: 'https://api.bilibili.com/x/web-interface/search/type?' + qs,
            method: 'GET',
            headers: {
                'Cookie': 'buvid3=0',
                'Referer': 'https://search.bilibili.com/'
            },
            timeout: 10000
        });

        var results = [];
        if (resp.status === 200 && resp.data && resp.data.data && resp.data.data.result) {
            var items = resp.data.data.result;
            for (var i = 0; i < items.length && results.length < pageSize; i++) {
                var item = items[i];
                if (!item.bvid) continue;

                var title = this._htmlDecode(item.title);
                var artist = this._htmlDecode(item.author);
                var duration = this._parseDuration(item.duration);

                results.push(this.buildResult({
                    id: 'v_' + item.bvid,
                    name: title,
                    artist: artist,
                    album: '',
                    duration: duration,
                    quality: ['128'],
                    needVip: false
                }));
            }
        }
        return results;
    } catch (e) {
        console.log('[bilibili] 搜索失败:', e.message);
        return [];
    }
};

/**
 * 解析 B 站时长格式 "mm:ss" 或 "hh:mm:ss"
 */
BilibiliProvider.prototype._parseDuration = function (durationStr) {
    if (!durationStr) return 0;
    var parts = String(durationStr).split(':');
    if (parts.length === 2) {
        return parseInt(parts[0], 10) * 60 + parseInt(parts[1], 10);
    } else if (parts.length === 3) {
        return parseInt(parts[0], 10) * 3600 + parseInt(parts[1], 10) * 60 + parseInt(parts[2], 10);
    }
    return parseInt(durationStr, 10) || 0;
};

/**
 * 获取歌曲播放链接
 */
BilibiliProvider.prototype.getSongUrl = async function (songId, quality) {
    // songId 格式: "v_BVxxxx" (视频) 或 "a_12345" (音频区)
    if (songId.indexOf('v_') === 0) {
        return this._getVideoAudioUrl(songId.slice(2));
    } else if (songId.indexOf('a_') === 0) {
        return this._getAudioUrl(songId.slice(2));
    }
    // 兼容旧格式: 直接当 bvid
    return this._getVideoAudioUrl(songId);
};

/**
 * 从视频中提取音频流 (DASH)
 */
BilibiliProvider.prototype._getVideoAudioUrl = async function (bvid) {
    try {
        // 1. 获取视频 cid
        var viewResp = await http.request({
            url: 'https://api.bilibili.com/x/web-interface/view?bvid=' + encodeURIComponent(bvid),
            method: 'GET',
            headers: {
                'Referer': 'https://www.bilibili.com/',
                'Cookie': 'buvid3=0'
            },
            timeout: 10000
        });

        if (!viewResp.data || !viewResp.data.data || !viewResp.data.data.pages) {
            console.log('[bilibili] 无法获取视频信息, bvid=' + bvid);
            return null;
        }

        var cid = viewResp.data.data.pages[0].cid;

        // 2. 获取 DASH 音频流 (fnval=16 表示 DASH 格式)
        var playResp = await http.request({
            url: 'https://api.bilibili.com/x/player/playurl?bvid=' + encodeURIComponent(bvid) + '&cid=' + cid + '&fnval=16',
            method: 'GET',
            headers: {
                'Referer': 'https://www.bilibili.com/' + bvid,
                'Cookie': 'buvid3=0'
            },
            timeout: 10000
        });

        if (playResp.data && playResp.data.data && playResp.data.data.dash &&
            playResp.data.data.dash.audio && playResp.data.data.dash.audio.length > 0) {
            var audioUrl = playResp.data.data.dash.audio[0].baseUrl ||
                           playResp.data.data.dash.audio[0].base_url;
            if (audioUrl) {
                console.log('[bilibili] 视频音频流获取成功');
                return {
                    url: audioUrl,
                    quality: 128,
                    format: 'm4a',
                    size: 0,
                    // B 站音频需要 Referer 才能播放
                    headers: {
                        'Referer': 'https://www.bilibili.com/'
                    }
                };
            }
        }
    } catch (e) {
        console.log('[bilibili] 视频音频流获取失败:', e.message);
    }
    return null;
};

/**
 * 从 B 站音频区获取播放链接
 */
BilibiliProvider.prototype._getAudioUrl = async function (audioId) {
    try {
        var resp = await http.request({
            url: 'https://www.bilibili.com/audio/music-service-c/web/url?sid=' + encodeURIComponent(audioId),
            method: 'GET',
            headers: {
                'Referer': 'https://www.bilibili.com/audio/'
            },
            timeout: 10000
        });

        if (resp.data && resp.data.code === 0 && resp.data.data && resp.data.data.cdns) {
            var cdnUrl = resp.data.data.cdns[0];
            if (cdnUrl) {
                console.log('[bilibili] 音频区获取成功');
                return {
                    url: cdnUrl,
                    quality: 128,
                    format: 'mp3',
                    size: 0
                };
            }
        }
    } catch (e) {
        console.log('[bilibili] 音频区获取失败:', e.message);
    }
    return null;
};

module.exports = new BilibiliProvider();
