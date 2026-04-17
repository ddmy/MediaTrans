'use strict';

/**
 * 搜索聚合服务
 * - 并发搜索多个平台
 * - 跨平台去重合并
 * - 错误隔离
 * - 超时保护
 */

var providers = require('../providers');

var SEARCH_TIMEOUT = 8000; // 单平台搜索超时(ms)

/**
 * 并发搜索所有平台，按歌名+作者去重合并
 * @param {string} keyword - 搜索关键词
 * @param {string[]|null} platformNames - 平台名列表，null 表示全部
 * @param {number} pageSize - 每平台最大结果数
 * @returns {{ merged: Array, platformStatus: Object }}
 */
async function search(keyword, platformNames, pageSize) {
    var targetProviders = providers.getByNames(platformNames);
    pageSize = pageSize || 30;

    // 并发搜索，单个平台异常不影响其他
    var searchPromises = targetProviders.map(function (provider) {
        return searchWithTimeout(provider, keyword, pageSize)
            .then(function (results) {
                return { platform: provider.name, displayName: provider.displayName, status: 'success', results: results };
            })
            .catch(function (err) {
                console.log('[aggregator] ' + provider.name + ' 搜索失败:', err.message);
                return { platform: provider.name, displayName: provider.displayName, status: 'error', error: err.message, results: [] };
            });
    });

    var searchResults = await Promise.all(searchPromises);

    // 构建平台状态
    var platformStatus = {};
    searchResults.forEach(function (sr) {
        platformStatus[sr.platform] = {
            name: sr.platform,
            displayName: sr.displayName,
            status: sr.status,
            count: sr.results.length,
            error: sr.error || null
        };
    });

    // 收集所有结果
    var allResults = [];
    searchResults.forEach(function (sr) {
        allResults = allResults.concat(sr.results);
    });

    // 跨平台去重合并
    var merged = mergeResults(allResults, keyword);

    return {
        keyword: keyword,
        merged: merged,
        platformStatus: platformStatus,
        totalCount: merged.length
    };
}

/**
 * 带超时的搜索
 */
function searchWithTimeout(provider, keyword, pageSize) {
    return new Promise(function (resolve, reject) {
        var timer = setTimeout(function () {
            reject(new Error('搜索超时'));
        }, SEARCH_TIMEOUT);

        provider.search(keyword, pageSize)
            .then(function (results) {
                clearTimeout(timer);
                resolve(results);
            })
            .catch(function (err) {
                clearTimeout(timer);
                reject(err);
            });
    });
}

/**
 * 跨平台去重合并
 * 策略：按 "标准化歌名-标准化作者" 做匹配，相同歌曲合并为一行
 */
function mergeResults(allResults, keyword) {
    var groups = {}; // matchKey → { primary, sources }
    var order = [];  // 保持顺序

    for (var i = 0; i < allResults.length; i++) {
        var item = allResults[i];
        var key = buildMatchKey(item.name, item.artist);

        if (!groups[key]) {
            groups[key] = {
                name: item.name,
                artist: item.artist,
                album: item.album,
                duration: item.duration,
                durationText: item.durationText,
                sources: [],
                matchScore: calcMatchScore(item.name, item.artist, keyword)
            };
            order.push(key);
        }

        // 添加此平台的源信息
        groups[key].sources.push({
            platform: item.platform,
            platformName: item.platformName,
            id: item.id,
            quality: item.quality,
            needVip: item.needVip,
            duration: item.duration || 0,
            durationText: item.durationText || '00:00',
            // 传递额外信息
            _hash: item._hash,
            _sqHash: item._sqHash,
            _320Hash: item._320Hash,
            _albumId: item._albumId,
            _copyrightId: item._copyrightId,
            _contentId: item._contentId,
            _mp3Url: item._mp3Url,
            _hqUrl: item._hqUrl,
            _sqUrl: item._sqUrl,
            _listenUrl: item._listenUrl
        });

        // 更新时长（取第一个非零时长，避免被错误合并的长版本覆盖）
        if (groups[key].duration === 0 && item.duration > 0) {
            groups[key].duration = item.duration;
            groups[key].durationText = item.durationText;
        }

        // 更新专辑名（取非空的）
        if (!groups[key].album && item.album) {
            groups[key].album = item.album;
        }
    }

    // 按匹配度排序
    var merged = order.map(function (key) { return groups[key]; });
    merged.sort(function (a, b) {
        return b.matchScore - a.matchScore;
    });

    return merged;
}

/**
 * 构建匹配键（用于去重）
 */
function buildMatchKey(name, artist) {
    var n = normalize(name);
    var a = normalize(artist);
    return n + '|' + a;
}

/**
 * 标准化字符串（去除空格、标点、大小写统一）
 */
function normalize(str) {
    if (!str) return '';
    return str.toLowerCase()
        .replace(/\s+/g, '')
        .replace(/[\(\)\[\]（）【】《》\-_\.\,\，\。\/\\]/g, '')
        .replace(/['"'"]/g, '')
        .trim();
}

/**
 * 计算与关键词的匹配度
 */
function calcMatchScore(name, artist, keyword) {
    var score = 0;
    var kw = keyword.toLowerCase();
    var n = (name || '').toLowerCase();
    var a = (artist || '').toLowerCase();

    // 精确匹配歌名
    if (n === kw) score += 100;
    // 歌名包含关键词
    else if (n.indexOf(kw) >= 0) score += 80;
    // 关键词包含歌名
    else if (kw.indexOf(n) >= 0) score += 60;

    // 作者匹配
    if (a.indexOf(kw) >= 0 || kw.indexOf(a) >= 0) score += 30;

    // 组合匹配（"歌手 歌名" 或 "歌名 歌手"）
    var parts = kw.split(/\s+/);
    if (parts.length >= 2) {
        for (var i = 0; i < parts.length; i++) {
            if (n.indexOf(parts[i]) >= 0) score += 20;
            if (a.indexOf(parts[i]) >= 0) score += 15;
        }
    }

    return score;
}

/**
 * 跨平台 URL 回退：当指定平台无法获取播放链接时，
 * 在其他平台搜索同名歌曲并尝试获取播放链接
 *
 * @param {string} songName - 歌曲名
 * @param {string} artist - 艺术家
 * @param {string} excludePlatform - 排除的平台（已经尝试失败的）
 * @param {string} quality - 音质
 * @returns {Promise<Object|null>} - { url, quality, format, size, fallbackPlatform }
 */
async function getSongUrlWithFallback(songName, artist, excludePlatform, quality) {
    // 回退优先级：酷我 > 咪咕 > 网易 > QQ > 酷狗
    var fallbackOrder = ['kuwo', 'migu', 'netease', 'taihe', 'bilibili', 'qq', 'kugou'];
    var allProviders = providers.list();

    var searchKeyword = songName;
    if (artist) {
        searchKeyword = artist + ' ' + songName;
    }

    for (var i = 0; i < fallbackOrder.length; i++) {
        var platformName = fallbackOrder[i];
        if (platformName === excludePlatform) continue;

        var provider = providers.get(platformName);
        if (!provider) continue;

        try {
            // 在备选平台搜索
            var results = await searchWithTimeout(provider, searchKeyword, 5);
            if (!results || results.length === 0) continue;

            // 找到最匹配的结果
            var bestMatch = null;
            var bestScore = 0;
            var normalizedName = normalize(songName);
            var normalizedArtist = normalize(artist);

            for (var j = 0; j < results.length; j++) {
                var r = results[j];
                var rName = normalize(r.name);
                var rArtist = normalize(r.artist);
                var score = 0;

                // 歌名匹配
                if (rName === normalizedName) score += 100;
                else if (rName.indexOf(normalizedName) >= 0 || normalizedName.indexOf(rName) >= 0) score += 60;
                else continue; // 歌名完全不匹配则跳过

                // 歌手匹配
                if (normalizedArtist && rArtist) {
                    if (rArtist === normalizedArtist) score += 50;
                    else if (rArtist.indexOf(normalizedArtist) >= 0 || normalizedArtist.indexOf(rArtist) >= 0) score += 30;
                }

                if (score > bestScore) {
                    bestScore = score;
                    bestMatch = r;
                }
            }

            if (!bestMatch || bestScore < 60) continue;

            // 尝试获取播放链接
            console.log('[fallback] 尝试从 ' + platformName + ' 获取: ' + bestMatch.name + ' - ' + bestMatch.artist);
            var urlResult = await provider.getSongUrl(bestMatch.id, quality);
            if (urlResult && urlResult.url) {
                urlResult.fallbackPlatform = platformName;
                console.log('[fallback] 从 ' + platformName + ' 获取成功');
                return urlResult;
            }

            // 降级到128
            if (quality !== '128') {
                urlResult = await provider.getSongUrl(bestMatch.id, '128');
                if (urlResult && urlResult.url) {
                    urlResult.fallbackPlatform = platformName;
                    console.log('[fallback] 从 ' + platformName + ' 获取成功 (降级128)');
                    return urlResult;
                }
            }
        } catch (e) {
            console.log('[fallback] ' + platformName + ' 回退失败:', e.message);
            continue;
        }
    }

    return null;
}

module.exports = {
    search: search,
    getSongUrlWithFallback: getSongUrlWithFallback
};
