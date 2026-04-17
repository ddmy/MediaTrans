'use strict';

/**
 * MediaTrans 音乐搜索 API 服务
 * 轻量级 HTTP 服务，不依赖 Express，使用 Node.js 内置 http 模块
 */

const http = require('http');
const url = require('url');
const { handleRequest } = require('./router');

const DEFAULT_PORT = 35200;

function getPort() {
    const args = process.argv.slice(2);
    for (let i = 0; i < args.length; i++) {
        if (args[i] === '--port' && args[i + 1]) {
            return parseInt(args[i + 1], 10) || DEFAULT_PORT;
        }
    }
    return DEFAULT_PORT;
}

const port = getPort();

const server = http.createServer(function (req, res) {
    // CORS headers for local development
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
    res.setHeader('Content-Type', 'application/json; charset=utf-8');

    if (req.method === 'OPTIONS') {
        res.writeHead(204);
        res.end();
        return;
    }

    handleRequest(req, res).catch(function (err) {
        console.error('[ERROR]', err.message || err);
        res.writeHead(500);
        res.end(JSON.stringify({ error: '服务器内部错误', message: err.message }));
    });
});

server.listen(port, '127.0.0.1', function () {
    console.log('[music-server] 启动成功，监听 http://127.0.0.1:' + port);
    console.log('[music-server] 版本: ' + require('./version.json').version);
});

server.on('error', function (err) {
    if (err.code === 'EADDRINUSE') {
        console.error('[music-server] 端口 ' + port + ' 已被占用');
        process.exit(1);
    }
    console.error('[music-server] 服务器错误:', err.message);
    process.exit(1);
});

// 优雅关闭
process.on('SIGTERM', function () {
    console.log('[music-server] 收到 SIGTERM，正在关闭...');
    server.close(function () { process.exit(0); });
});

process.on('SIGINT', function () {
    console.log('[music-server] 收到 SIGINT，正在关闭...');
    server.close(function () { process.exit(0); });
});
