'use strict';

/**
 * Provider 基类
 * 所有音乐平台 Provider 继承此类
 */

function BaseProvider(name, displayName) {
    this.name = name;
    this.displayName = displayName;
}

/**
 * 搜索歌曲
 * @param {string} keyword - 搜索关键词
 * @param {number} pageSize - 每页数量
 * @returns {Promise<Array<{id, name, artist, album, duration, platform, platformName, quality}>>}
 */
BaseProvider.prototype.search = function (keyword, pageSize) {
    throw new Error(this.name + ' provider 未实现 search 方法');
};

/**
 * 获取歌曲播放链接
 * @param {string} songId - 歌曲 ID
 * @param {string} quality - 音质 (128/320/flac)
 * @returns {Promise<{url, quality, format, size}>}
 */
BaseProvider.prototype.getSongUrl = function (songId, quality) {
    throw new Error(this.name + ' provider 未实现 getSongUrl 方法');
};

/**
 * 格式化时长 (秒 → mm:ss)
 */
BaseProvider.prototype.formatDuration = function (seconds) {
    if (!seconds || seconds <= 0) return '00:00';
    var m = Math.floor(seconds / 60);
    var s = Math.floor(seconds % 60);
    return (m < 10 ? '0' : '') + m + ':' + (s < 10 ? '0' : '') + s;
};

/**
 * 构建标准化的搜索结果项
 */
BaseProvider.prototype.buildResult = function (opts) {
    return {
        id: String(opts.id || ''),
        name: opts.name || '',
        artist: opts.artist || '未知',
        album: opts.album || '',
        duration: opts.duration || 0,
        durationText: this.formatDuration(opts.duration),
        platform: this.name,
        platformName: this.displayName,
        quality: opts.quality || ['128', '320'],
        needVip: opts.needVip || false
    };
};

module.exports = BaseProvider;
