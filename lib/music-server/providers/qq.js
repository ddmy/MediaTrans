'use strict';

/**
 * QQ 音乐 Provider
 *
 * 搜索: GET c.y.qq.com/soso/fcgi-bin/client_search_cp
 * 播放链接: POST u.y.qq.com/cgi-bin/musicu.fcg (vkey)
 */

var BaseProvider = require('./base');
var http = require('../utils/http');

function QQMusicProvider() {
    BaseProvider.call(this, 'qq', 'QQ音乐');
}

QQMusicProvider.prototype = Object.create(BaseProvider.prototype);
QQMusicProvider.prototype.constructor = QQMusicProvider;

/**
 * 搜索歌曲 — 使用 client_search_cp 接口
 */
QQMusicProvider.prototype.search = async function (keyword, pageSize) {
    pageSize = pageSize || 30;

    var resp = await http.request({
        url: 'https://c.y.qq.com/soso/fcgi-bin/client_search_cp?w=' +
            encodeURIComponent(keyword) +
            '&p=1&n=' + pageSize + '&format=json',
        method: 'GET',
        referer: 'https://y.qq.com/',
        timeout: 8000
    });

    var results = [];
    if (resp.status === 200 && resp.data && resp.data.data && resp.data.data.song) {
        var songs = resp.data.data.song.list || [];
        for (var i = 0; i < songs.length; i++) {
            var song = songs[i];
            var artists = (song.singer || []).map(function (s) { return s.name; }).join('/');
            var albumName = song.albumname || '';
            var mid = song.songmid || '';
            var interval = song.interval || 0;

            var qualityList = ['128', '320'];
            if (song.size320 > 0) qualityList = ['128', '320'];
            if (song.sizeflac > 0) qualityList.push('flac');

            var needVip = song.pay && song.pay.payplay === 1;

            results.push(this.buildResult({
                id: mid,
                name: song.songname || song.songname_hilight || '',
                artist: artists,
                album: albumName,
                duration: interval,
                quality: qualityList,
                needVip: needVip
            }));
        }
    }
    return results;
};

/**
 * 获取歌曲播放链接
 * 参考 Listen1: 默认使用 128kbps (M500)，高品质 QQ 通常拒绝
 */
QQMusicProvider.prototype.getSongUrl = async function (songId, quality) {
    var typeMap = {
        '128': { prefix: 'M500', ext: 'mp3', type: 'size_128mp3' },
        '320': { prefix: 'M800', ext: 'mp3', type: 'size_320mp3' },
        'flac': { prefix: 'F000', ext: 'flac', type: 'size_flac' }
    };

    // Listen1 策略：默认 128kbps, QQ 服务器对高品质限制严格
    var effectiveQuality = quality || '128';
    var qInfo = typeMap[effectiveQuality] || typeMap['128'];
    var filename = qInfo.prefix + songId + '.' + qInfo.ext;
    // Listen1 使用固定 guid '10000'
    var guid = '10000';

    var reqData = {
        'comm': {
            'ct': 24,
            'cv': 0
        },
        'url_mid': {
            'module': 'vkey.GetVkeyServer',
            'method': 'CgiGetVkey',
            'param': {
                'guid': guid,
                'songmid': [songId],
                'songtype': [0],
                'uin': '0',
                'loginflag': 1,
                'platform': '20',
                'filename': [filename]
            }
        }
    };

    var resp = await http.request({
        url: 'https://u.y.qq.com/cgi-bin/musicu.fcg',
        method: 'POST',
        data: JSON.stringify(reqData),
        headers: {
            'Content-Type': 'application/json',
            'Origin': 'https://y.qq.com'
        },
        referer: 'https://y.qq.com/',
        timeout: 10000
    });

    if (resp.status === 200 && resp.data) {
        var urlData = resp.data.url_mid;
        if (urlData && urlData.data && urlData.data.midurlinfo) {
            var info = urlData.data.midurlinfo[0];
            var purl = info ? info.purl : '';
            if (purl) {
                var sip = urlData.data.sip && urlData.data.sip[0] ? urlData.data.sip[0] : 'https://dl.stream.qqmusic.qq.com/';
                return {
                    url: sip + purl,
                    quality: parseInt(effectiveQuality) || 128,
                    format: qInfo.ext,
                    size: 0
                };
            }
        }
    }

    // 如果请求的不是128kbps且失败了，降级到128kbps
    if (effectiveQuality !== '128') {
        return this.getSongUrl(songId, '128');
    }
    return null;
};

module.exports = new QQMusicProvider();
