'use strict';

const url = require('url');
const querystring = require('querystring');
const aggregator = require('./services/aggregator');
const providers = require('./providers');

/**
 * 路由处理
 */
async function handleRequest(req, res) {
    var parsed = url.parse(req.url, true);
    var pathname = parsed.pathname;
    var query = parsed.query;

    // GET /api/health
    if (pathname === '/api/health') {
        var ver = require('./version.json');
        res.writeHead(200);
        res.end(JSON.stringify({ status: 'ok', version: ver.version }));
        return;
    }

    // GET /api/platforms
    if (pathname === '/api/platforms') {
        var platforms = providers.list().map(function (p) {
            return { name: p.name, displayName: p.displayName, enabled: true };
        });
        res.writeHead(200);
        res.end(JSON.stringify({ platforms: platforms }));
        return;
    }

    // GET /api/search?keyword=xxx&platform=all
    if (pathname === '/api/search') {
        var keyword = query.keyword || '';
        var platform = query.platform || 'all';
        var pageSize = parseInt(query.pageSize, 10) || 30;

        if (!keyword.trim()) {
            res.writeHead(400);
            res.end(JSON.stringify({ error: '请输入搜索关键词' }));
            return;
        }

        var platformList = null;
        if (platform && platform !== 'all') {
            platformList = platform.split(',').map(function (s) { return s.trim(); });
        }

        var results = await aggregator.search(keyword.trim(), platformList, pageSize);
        res.writeHead(200);
        res.end(JSON.stringify(results));
        return;
    }

    // GET /api/song/url?id=xxx&platform=netease&quality=320&name=xxx&artist=xxx
    if (pathname === '/api/song/url') {
        var songId = query.id || '';
        var songPlatform = query.platform || '';
        var quality = query.quality || '320';
        var songName = query.name || '';
        var songArtist = query.artist || '';

        if (!songId || !songPlatform) {
            res.writeHead(400);
            res.end(JSON.stringify({ error: '缺少 id 或 platform 参数' }));
            return;
        }

        var provider = providers.get(songPlatform);
        if (!provider) {
            res.writeHead(400);
            res.end(JSON.stringify({ error: '不支持的平台: ' + songPlatform }));
            return;
        }

        try {
            // 1. 尝试指定平台
            var streamInfo = await provider.getSongUrl(songId, quality);

            // 2. 指定平台失败，降级到128
            if (!streamInfo || !streamInfo.url) {
                if (quality !== '128') {
                    streamInfo = await provider.getSongUrl(songId, '128');
                }
            }

            // 3. 仍然失败，尝试跨平台回退
            if ((!streamInfo || !streamInfo.url) && songName) {
                console.log('[router] ' + songPlatform + ' 获取失败，启动跨平台回退: ' + songName);
                streamInfo = await aggregator.getSongUrlWithFallback(songName, songArtist, songPlatform, quality);
            }

            if (!streamInfo || !streamInfo.url) {
                res.writeHead(404);
                res.end(JSON.stringify({ error: '无法获取播放链接，所有平台均失败' }));
                return;
            }

            res.writeHead(200);
            res.end(JSON.stringify(streamInfo));
        } catch (err) {
            // 异常时也尝试跨平台回退
            if (songName) {
                try {
                    var fallbackInfo = await aggregator.getSongUrlWithFallback(songName, songArtist, songPlatform, quality);
                    if (fallbackInfo && fallbackInfo.url) {
                        res.writeHead(200);
                        res.end(JSON.stringify(fallbackInfo));
                        return;
                    }
                } catch (e2) {
                    // ignore fallback error
                }
            }
            res.writeHead(500);
            res.end(JSON.stringify({ error: '获取播放链接失败: ' + err.message }));
        }
        return;
    }

    // 404
    res.writeHead(404);
    res.end(JSON.stringify({ error: '未知接口: ' + pathname }));
}

module.exports = { handleRequest: handleRequest };
