'use strict';

/**
 * 酷狗音乐 Provider
 *
 * 搜索: GET complexsearch.kugou.com/v2/search/song
 * 播放链接: GET wwwapi.kugou.com/play/songinfo
 */

var BaseProvider = require('./base');
var http = require('../utils/http');
var cryptoUtil = require('../utils/crypto');

function KugouProvider() {
    BaseProvider.call(this, 'kugou', '酷狗音乐');
}

KugouProvider.prototype = Object.create(BaseProvider.prototype);
KugouProvider.prototype.constructor = KugouProvider;

/**
 * 生成酷狗签名
 */
KugouProvider.prototype._sign = function (params) {
    var keys = Object.keys(params).sort();
    var str = 'NVPh5oo715z5DIWAeQlhMDsWXXQV4hwt';
    for (var i = 0; i < keys.length; i++) {
        str += keys[i] + '=' + params[keys[i]];
    }
    str += 'NVPh5oo715z5DIWAeQlhMDsWXXQV4hwt';
    return cryptoUtil.md5(str);
};

/**
 * 搜索歌曲
 */
KugouProvider.prototype.search = async function (keyword, pageSize) {
    pageSize = pageSize || 30;

    var params = {
        callback: '',
        srcappid: '2919',
        clientver: '1000',
        clienttime: String(Date.now()),
        mid: cryptoUtil.randomHex(32),
        uuid: cryptoUtil.randomHex(32),
        dfid: '-',
        keyword: keyword,
        page: '1',
        pagesize: String(pageSize),
        bitrate: '0',
        isfuzzy: '0',
        inputtype: '0',
        platform: 'WebFilter',
        userid: '0',
        iscorrection: '1',
        privilege_filter: '0',
        filter: '10',
        token: '',
        appid: '1014'
    };

    params.signature = this._sign(params);

    var qs = Object.keys(params).map(function (k) {
        return encodeURIComponent(k) + '=' + encodeURIComponent(params[k]);
    }).join('&');

    var resp = await http.request({
        url: 'https://complexsearch.kugou.com/v2/search/song?' + qs,
        method: 'GET',
        referer: 'https://www.kugou.com/',
        timeout: 8000
    });

    var results = [];
    if (resp.status === 200 && resp.data && resp.data.data && resp.data.data.lists) {
        var songs = resp.data.data.lists;
        for (var i = 0; i < songs.length; i++) {
            var song = songs[i];
            var songName = (song.SongName || song.FileName || '').replace(/<[^>]+>/g, '');
            var artist = (song.SingerName || '').replace(/<[^>]+>/g, '');

            // 清理歌名中 "歌手 - 歌名" 的格式
            if (songName.indexOf(' - ') > 0 && !artist) {
                var parts = songName.split(' - ');
                artist = parts[0].trim();
                songName = parts.slice(1).join(' - ').trim();
            }

            var qualityList = ['128', '320'];
            if (song.SQFileHash && song.SQFileHash !== '00000000000000000000000000000000') {
                qualityList.push('flac');
            }

            results.push(this.buildResult({
                id: song.FileHash + '|' + (song.AlbumID || '0'),
                name: songName,
                artist: artist,
                album: song.AlbumName || '',
                duration: song.Duration || 0,
                quality: qualityList,
                needVip: song.PayType === 3
            }));

            // 保存 hash 用于获取播放链接
            var r = results[results.length - 1];
            r._hash = song.FileHash;
            r._sqHash = song.SQFileHash;
            r._320Hash = song.HQFileHash || song.FileHash;
            r._albumId = song.AlbumID;
        }
    }
    return results;
};

/**
 * 获取歌曲播放链接 — 使用手机端 API (参考 Listen1，更可靠)
 */
KugouProvider.prototype.getSongUrl = async function (songId, quality) {
    // songId 格式: "hash|albumId" 或 EMixSongID
    var hash = '';
    var albumId = '0';
    if (songId.indexOf('|') > 0) {
        var parts = songId.split('|');
        hash = parts[0];
        albumId = parts[1] || '0';
    }

    // EMixSongID 格式：需要先获取 hash
    if (!hash) {
        var hashInfo = await this._getHashFromMixId(songId, quality);
        if (hashInfo) {
            hash = hashInfo.hash;
            albumId = hashInfo.albumId || albumId;
        }
    }

    if (!hash) {
        console.log('[kugou] 无法获取 hash, songId=' + songId);
        return null;
    }

    // 方案1：手机端 API (Listen1 方案，限制最松)
    var urlResult = await this._tryMobileApi(hash);
    if (urlResult) return urlResult;

    // 方案2：桌面端 API (旧方案)
    urlResult = await this._tryDesktopApi(hash, albumId);
    if (urlResult) return urlResult;

    return null;
};

/**
 * 从 EMixSongID 获取 hash
 */
KugouProvider.prototype._getHashFromMixId = async function (mixId, quality) {
    try {
        var resp = await http.request({
            url: 'https://www.kugou.com/song/#hash=&album_id=0&album_audio_id=0&mixsongid=' + mixId,
            method: 'GET',
            referer: 'https://www.kugou.com/',
            timeout: 8000,
            responseType: 'text'
        });

        if (resp.data && typeof resp.data === 'string') {
            // 尝试从页面提取 hash
            var hashMatch = /\"hash\"\s*:\s*\"([a-fA-F0-9]{32})\"/.exec(resp.data);
            var albumMatch = /\"album_id\"\s*:\s*(\d+)/.exec(resp.data);
            if (hashMatch) {
                return {
                    hash: hashMatch[1],
                    albumId: albumMatch ? albumMatch[1] : '0'
                };
            }
        }
    } catch (e) {
        console.log('[kugou] 获取 hash 失败:', e.message);
    }

    // 备用：通过搜索 API 的 privilege 接口获取
    try {
        var resp2 = await http.request({
            url: 'https://wwwapi.kugou.com/yy/index.php?r=play/getdata&hash=&mid=' +
                cryptoUtil.randomHex(32) + '&platid=4&album_id=0&_=' + Date.now() +
                '&mixsongid=' + mixId,
            method: 'GET',
            referer: 'https://www.kugou.com/',
            headers: { 'Cookie': 'kg_mid=' + cryptoUtil.randomHex(32) },
            timeout: 10000
        });

        if (resp2.data && resp2.data.data) {
            var d = resp2.data.data;
            if (d.hash) {
                return { hash: d.hash, albumId: d.album_id ? String(d.album_id) : '0' };
            }
        }
    } catch (e2) {
        console.log('[kugou] 备用获取 hash 失败:', e2.message);
    }

    return null;
};

/**
 * 手机端 API — Listen1 使用的接口，限制较松
 */
KugouProvider.prototype._tryMobileApi = async function (hash) {
    try {
        var resp = await http.request({
            url: 'https://m.kugou.com/app/i/getSongInfo.php?cmd=playInfo&hash=' + hash,
            method: 'GET',
            referer: 'https://m.kugou.com/',
            timeout: 10000
        });

        if (resp.status === 200 && resp.data) {
            var data = resp.data;
            var url = data.url || '';
            if (url && (url.indexOf('http://') === 0 || url.indexOf('https://') === 0)) {
                console.log('[kugou] 手机端 API 获取成功, bitrate=' + (data.bitRate || 'unknown'));
                return {
                    url: url,
                    quality: data.bitRate ? Math.round(data.bitRate / 1000) : 128,
                    format: url.indexOf('.flac') > -1 ? 'flac' : 'mp3',
                    size: data.fileSize || 0
                };
            }
        }
    } catch (e) {
        console.log('[kugou] 手机端 API 请求失败:', e.message);
    }
    return null;
};

/**
 * 桌面端 API — 旧方案，需要 cookie
 */
KugouProvider.prototype._tryDesktopApi = async function (hash, albumId) {
    try {
        var params = {
            r: 'play/getdata',
            hash: hash,
            mid: cryptoUtil.randomHex(32),
            platid: '4',
            album_id: albumId,
            _: String(Date.now())
        };

        var resp = await http.request({
            url: 'https://wwwapi.kugou.com/yy/index.php?' + Object.keys(params).map(function (k) {
                return k + '=' + encodeURIComponent(params[k]);
            }).join('&'),
            method: 'GET',
            referer: 'https://www.kugou.com/',
            headers: {
                'Cookie': 'kg_mid=' + cryptoUtil.randomHex(32)
            },
            timeout: 10000
        });

        if (resp.status === 200 && resp.data && resp.data.data) {
            var data = resp.data.data;
            if (data.play_url || data.play_backup_url) {
                return {
                    url: data.play_url || data.play_backup_url,
                    quality: data.bitrate ? Math.round(data.bitrate / 1000) : 128,
                    format: (data.play_url || '').indexOf('.flac') > -1 ? 'flac' : 'mp3',
                    size: data.filesize || 0
                };
            }
        }
    } catch (e) {
        console.log('[kugou] 桌面端 API 请求失败:', e.message);
    }
    return null;
};

module.exports = new KugouProvider();
