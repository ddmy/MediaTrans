'use strict';

/**
 * 咪咕音乐 Provider
 * ★ 免费曲库最全，VIP 突破的最佳备选源
 *
 * 搜索: GET app.c.nf.migu.cn search_all.do
 * 播放链接: 通过 copyrightId 构造 CDN 链接
 */

var BaseProvider = require('./base');
var http = require('../utils/http');
var cryptoUtil = require('../utils/crypto');

function MiguProvider() {
    BaseProvider.call(this, 'migu', '咪咕音乐');
}

MiguProvider.prototype = Object.create(BaseProvider.prototype);
MiguProvider.prototype.constructor = MiguProvider;

/**
 * 搜索歌曲 — 使用 search_all.do 接口
 */
MiguProvider.prototype.search = async function (keyword, pageSize) {
    pageSize = pageSize || 30;

    var searchSwitch = JSON.stringify({ song: 1 });
    var resp = await http.request({
        url: 'https://app.c.nf.migu.cn/MIGUM2.0/v1.0/content/search_all.do?' +
            'ua=Android_migu&version=5.0.1&text=' + encodeURIComponent(keyword) +
            '&pageNo=1&pageSize=' + pageSize +
            '&searchSwitch=' + encodeURIComponent(searchSwitch),
        method: 'GET',
        headers: {
            'channel': '0146951',
            'uid': String(Date.now())
        },
        referer: 'https://m.music.migu.cn/',
        timeout: 8000
    });

    var results = [];
    if (resp.status === 200 && resp.data && resp.data.songResultData && resp.data.songResultData.result) {
        var songs = resp.data.songResultData.result;
        for (var i = 0; i < songs.length; i++) {
            var song = songs[i];
            var songName = song.name || '';
            var singerList = song.singers || [];
            var artist = singerList.map(function (s) { return s.name; }).join('/');
            var albumList = song.albums || [];
            var albumName = albumList.length > 0 ? (albumList[0].name || '') : '';

            // 音质
            var qualityList = ['128'];
            var newRateFmts = song.newRateFormats || song.rateFormats || [];
            var has320 = false;
            var hasFlac = false;
            for (var j = 0; j < newRateFmts.length; j++) {
                var fmt = newRateFmts[j];
                if (fmt.formatType === 'HQ' || fmt.formatType === 'SQ') has320 = true;
                if (fmt.formatType === 'SQ' || fmt.formatType === 'ZQ') hasFlac = true;
            }
            if (has320) qualityList.push('320');
            if (hasFlac) qualityList.push('flac');

            results.push(this.buildResult({
                id: song.copyrightId || song.id || song.songId,
                name: songName,
                artist: artist,
                album: albumName,
                duration: 0, // search_all.do 不返回时长
                quality: qualityList,
                needVip: false
            }));

            // 保存额外信息用于获取播放链接
            var r = results[results.length - 1];
            r._copyrightId = song.copyrightId;
            r._contentId = song.contentId || song.id;
            r._songId = song.songId || song.id;
            // 从 rateFormats 中提取已有的 URL
            if (newRateFmts.length > 0) {
                for (var k = 0; k < newRateFmts.length; k++) {
                    var rf = newRateFmts[k];
                    if (rf.url || rf.androidUrl) {
                        var u = rf.url || rf.androidUrl;
                        if (rf.formatType === 'PQ') r._pqUrl = u;
                        else if (rf.formatType === 'HQ') r._hqUrl = u;
                        else if (rf.formatType === 'SQ') r._sqUrl = u;
                    }
                }
            }
        }
    }
    return results;
};

/**
 * 获取歌曲播放链接
 */
MiguProvider.prototype.getSongUrl = async function (songId, quality) {
    // 尝试通过 listen-url API
    var urlResult = await this._tryListenUrl(songId, quality);
    if (urlResult) return urlResult;

    // 尝试通过歌曲详情 API
    urlResult = await this._tryDetailApi(songId, quality);
    if (urlResult) return urlResult;

    // 降级
    if (quality !== '128') {
        urlResult = await this._tryListenUrl(songId, '128');
        if (urlResult) return urlResult;
        urlResult = await this._tryDetailApi(songId, '128');
        if (urlResult) return urlResult;
    }

    return null;
};

/**
 * 通过 listen-url API
 */
MiguProvider.prototype._tryListenUrl = async function (songId, quality) {
    try {
        var toneFlag = quality === 'flac' ? 'SQ' : (quality === '320' ? 'HQ' : 'PQ');
        var resp = await http.request({
            url: 'https://app.c.nf.migu.cn/MIGUM2.0/strategy/listen-url/v2.4?' +
                'netType=01&resourceType=E&songId=' + songId +
                '&toneFlag=' + toneFlag,
            method: 'GET',
            headers: {
                'channel': '0146951',
                'uid': String(Date.now())
            },
            referer: 'https://m.music.migu.cn/',
            timeout: 10000
        });

        if (resp.status === 200 && resp.data && resp.data.data) {
            var data = resp.data.data;
            var url = data.url;
            if (url) {
                if (url.indexOf('//') === 0) url = 'https:' + url;
                if (url.indexOf('http://') === 0) url = url.replace('http://', 'https://');
                return {
                    url: url,
                    quality: parseInt(quality) || 128,
                    format: quality === 'flac' ? 'flac' : 'mp3',
                    size: 0
                };
            }
        }
    } catch (e) {
        console.log('[migu] listen-url 请求失败:', e.message);
    }
    return null;
};

/**
 * 通过歌曲详情 API 获取播放链接
 */
MiguProvider.prototype._tryDetailApi = async function (songId, quality) {
    try {
        var resp = await http.request({
            url: 'https://app.c.nf.migu.cn/MIGUM2.0/v2.0/content/listen-url?copyrightId=' +
                songId + '&contentId=&resourceType=2&netType=01&toneFlag=' +
                (quality === 'flac' ? 'SQ' : (quality === '320' ? 'HQ' : 'LQ')),
            method: 'GET',
            headers: {
                'channel': '0146951',
                'uid': String(Date.now())
            },
            referer: 'https://m.music.migu.cn/',
            timeout: 10000
        });

        if (resp.status === 200 && resp.data && resp.data.data) {
            var url = resp.data.data.url;
            if (url) {
                if (url.indexOf('//') === 0) url = 'https:' + url;
                if (url.indexOf('http://') === 0) url = url.replace('http://', 'https://');
                return {
                    url: url,
                    quality: parseInt(quality) || 128,
                    format: quality === 'flac' ? 'flac' : 'mp3',
                    size: 0
                };
            }
        }
    } catch (e) {
        console.log('[migu] detail API 请求失败:', e.message);
    }
    return null;
};

module.exports = new MiguProvider();
