'use strict';

/**
 * 网易云音乐 Provider
 *
 * 搜索: POST /api/search/get/web (无需加密)
 * 播放链接: GET /api/song/enhance/player/url
 */

var BaseProvider = require('./base');
var http = require('../utils/http');
var cryptoUtil = require('../utils/crypto');

function NeteaseProvider() {
    BaseProvider.call(this, 'netease', '网易云音乐');
    this._initialized = false;
}

NeteaseProvider.prototype = Object.create(BaseProvider.prototype);
NeteaseProvider.prototype.constructor = NeteaseProvider;

/**
 * 初始化：设置 Cookie
 */
NeteaseProvider.prototype._init = async function () {
    if (this._initialized) return;
    try {
        http.setCookies('music.163.com', {
            'os': 'pc',
            'appver': '2.10.11',
            'osver': 'Microsoft-Windows-10-Professional-build-22631-64bit',
            '_ntes_nuid': cryptoUtil.randomHex(32),
            'NMTID': cryptoUtil.randomHex(32)
        });
        this._initialized = true;
    } catch (e) {
        console.log('[netease] 初始化失败:', e.message);
    }
};

/**
 * 搜索歌曲 — 使用无需加密的 /api 接口
 */
NeteaseProvider.prototype.search = async function (keyword, pageSize) {
    await this._init();
    pageSize = pageSize || 30;

    var postBody = 's=' + encodeURIComponent(keyword) +
        '&type=1&limit=' + pageSize + '&offset=0';

    var resp = await http.request({
        url: 'https://music.163.com/api/search/get/web?csrf_token=',
        method: 'POST',
        data: postBody,
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'Origin': 'https://music.163.com'
        },
        referer: 'https://music.163.com/',
        timeout: 8000
    });

    var results = [];
    if (resp.status === 200 && resp.data && resp.data.result && resp.data.result.songs) {
        var songs = resp.data.result.songs;
        for (var i = 0; i < songs.length; i++) {
            var song = songs[i];
            var artists = (song.artists || song.ar || []).map(function (a) { return a.name; }).join('/');
            var albumObj = song.album || song.al || {};
            var albumName = albumObj.name || '';
            var fee = song.fee;

            var qualityList = ['128', '320'];
            if (song.hMusic || song.h) {
                qualityList.push('flac');
            }

            results.push(this.buildResult({
                id: song.id,
                name: song.name,
                artist: artists,
                album: albumName,
                duration: Math.round((song.duration || song.dt || 0) / 1000),
                quality: qualityList,
                needVip: fee === 1
            }));
        }
    }
    return results;
};

/**
 * 获取歌曲播放链接 — 使用 NeteaseCloudMusicApi (eapi 加密)
 */
NeteaseProvider.prototype.getSongUrl = async function (songId, quality) {
    await this._init();

    // 使用 NeteaseCloudMusicApi 的 song_url_v1 (eapi 加密，更可靠)
    var urlResult = await this._tryNcmApi(songId, quality);
    if (urlResult) return urlResult;

    // 降级到标准音质
    if (quality !== '128') {
        urlResult = await this._tryNcmApi(songId, '128');
        if (urlResult) return urlResult;
    }

    // 最终回退：使用旧版裸 API
    urlResult = await this._tryPlayerApi(songId, quality);
    if (urlResult) return urlResult;

    if (quality !== '128') {
        urlResult = await this._tryPlayerApi(songId, '128');
        if (urlResult) return urlResult;
    }
    return null;
};

/**
 * 通过 NeteaseCloudMusicApi (eapi) 获取 — 第三方成熟方案
 */
NeteaseProvider.prototype._tryNcmApi = async function (songId, quality) {
    try {
        var ncmApi = require('NeteaseCloudMusicApi');
        var levelMap = { '128': 'standard', '320': 'higher', 'flac': 'lossless' };
        var level = levelMap[quality] || 'standard';

        var result = await ncmApi.song_url_v1({
            id: songId,
            level: level
        });

        if (result && result.body && result.body.data) {
            var songData = Array.isArray(result.body.data) ? result.body.data[0] : result.body.data;
            if (songData && songData.url) {
                console.log('[netease] NeteaseCloudMusicApi 获取成功, br=' + (songData.br || 'unknown'));
                return {
                    url: songData.url,
                    quality: songData.br ? Math.round(songData.br / 1000) : parseInt(quality),
                    format: songData.type || 'mp3',
                    size: songData.size || 0
                };
            }
        }
    } catch (e) {
        console.log('[netease] NeteaseCloudMusicApi 请求失败:', e.message);
    }
    return null;
};

/**
 * 通过 player/url 接口获取
 */
NeteaseProvider.prototype._tryPlayerApi = async function (songId, quality) {
    try {
        var brMap = { '128': 128000, '320': 320000, 'flac': 999000 };
        var br = brMap[quality] || 320000;

        var resp = await http.request({
            url: 'https://music.163.com/api/song/enhance/player/url?ids=[' + songId + ']&br=' + br,
            method: 'GET',
            referer: 'https://music.163.com/',
            timeout: 10000
        });

        if (resp.status === 200 && resp.data && resp.data.data) {
            var songData = Array.isArray(resp.data.data) ? resp.data.data[0] : resp.data.data;
            if (songData && songData.url) {
                return {
                    url: songData.url,
                    quality: songData.br ? Math.round(songData.br / 1000) : parseInt(quality),
                    format: songData.type || 'mp3',
                    size: songData.size || 0
                };
            }
        }
    } catch (e) {
        console.log('[netease] player API 请求失败:', e.message);
    }
    return null;
};

module.exports = new NeteaseProvider();
