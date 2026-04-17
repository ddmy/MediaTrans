'use strict';

/**
 * 千千音乐 (太合音乐) Provider (参考 Listen1)
 *
 * 搜索: GET music.taihe.com/v1/search
 * 播放链接: GET music.taihe.com/v1/song/tracklink
 * 需要签名: appid + timestamp + md5(sorted_params + secret)
 */

var BaseProvider = require('./base');
var http = require('../utils/http');
var cryptoUtil = require('../utils/crypto');

var TAIHE_APPID = '16073360';
var TAIHE_SECRET = '0b50b02fd0d73a9c4c8c3a781c30845f';
var TAIHE_BASE = 'https://music.taihe.com/v1';

/**
 * 签名参数 — 参考 Listen1 taihe.js
 */
function signParams(params) {
    params.timestamp = String(Math.round(Date.now() / 1000));
    params.appid = TAIHE_APPID;

    // 按 key 排序拼接
    var keys = Object.keys(params).sort();
    var pairs = [];
    for (var i = 0; i < keys.length; i++) {
        pairs.push(keys[i] + '=' + params[keys[i]]);
    }
    var signStr = pairs.join('&') + TAIHE_SECRET;
    params.sign = cryptoUtil.md5(signStr);
    return params;
}

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

function TaiheProvider() {
    BaseProvider.call(this, 'taihe', '千千音乐');
}

TaiheProvider.prototype = Object.create(BaseProvider.prototype);
TaiheProvider.prototype.constructor = TaiheProvider;

/**
 * 搜索歌曲
 */
TaiheProvider.prototype.search = async function (keyword, pageSize) {
    pageSize = pageSize || 30;

    try {
        var params = signParams({
            word: keyword,
            pageNo: '1',
            type: '1'
        });

        var resp = await http.request({
            url: TAIHE_BASE + '/search?' + buildQuery(params),
            method: 'GET',
            referer: 'https://music.taihe.com/',
            timeout: 10000
        });

        var results = [];
        if (resp.status === 200 && resp.data && resp.data.data && resp.data.data.typeTrack) {
            var tracks = resp.data.data.typeTrack;
            for (var i = 0; i < tracks.length && results.length < pageSize; i++) {
                var song = tracks[i];
                var artist = '';
                if (song.artist && song.artist.length > 0) {
                    artist = song.artist[0].name;
                }

                results.push(this.buildResult({
                    id: String(song.id || song.assetId || ''),
                    name: song.title || '',
                    artist: artist,
                    album: song.albumTitle || '',
                    duration: song.duration || 0,
                    quality: ['128', '320'],
                    needVip: false
                }));
            }
        }
        return results;
    } catch (e) {
        console.log('[taihe] 搜索失败:', e.message);
        return [];
    }
};

/**
 * 获取歌曲播放链接
 */
TaiheProvider.prototype.getSongUrl = async function (songId, quality) {
    try {
        var params = signParams({
            TSID: String(songId)
        });

        var resp = await http.request({
            url: TAIHE_BASE + '/song/tracklink?' + buildQuery(params),
            method: 'GET',
            referer: 'https://music.taihe.com/',
            timeout: 10000
        });

        if (resp.status === 200 && resp.data && resp.data.data) {
            var data = resp.data.data;
            // 千千音乐 tracklink 接口可能不返回 path (需要 VIP)
            if (data.path) {
                console.log('[taihe] 获取成功, rate=' + (data.rate || 'unknown'));
                return {
                    url: data.path,
                    quality: data.rate || 128,
                    format: (data.path || '').indexOf('.flac') > -1 ? 'flac' : 'mp3',
                    size: data.size || 0
                };
            } else {
                console.log('[taihe] 无播放链接 (isVip=' + data.isVip + ', pay_model=' + data.pay_model + ')');
            }
        } else {
            console.log('[taihe] 请求失败, code=' + (resp.data ? resp.data.errno : 'unknown'));
        }
    } catch (e) {
        console.log('[taihe] 获取播放链接失败:', e.message);
    }
    return null;
};

module.exports = new TaiheProvider();
