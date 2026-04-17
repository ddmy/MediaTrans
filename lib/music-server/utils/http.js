'use strict';

/**
 * HTTP 请求封装
 * - User-Agent 轮换
 * - Cookie 管理
 * - 自动重试（指数退避）
 * - 请求限流
 */

var axios = require('axios');

// User-Agent 池
var UA_LIST = [
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36',
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0',
    'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
    'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Safari/537.36 Edg/118.0.2088.76',
    'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1',
    'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36'
];

// 每个域名的 cookie jar
var cookieJars = {};

// 请求限流：每个域名的上次请求时间
var lastRequestTime = {};
var MIN_INTERVAL = 500; // 最小请求间隔(ms)

function randomUA() {
    return UA_LIST[Math.floor(Math.random() * UA_LIST.length)];
}

function getDomain(urlStr) {
    try {
        var u = new URL(urlStr);
        return u.hostname;
    } catch (e) {
        return 'unknown';
    }
}

/**
 * 等待限流间隔
 */
function waitThrottle(domain) {
    var last = lastRequestTime[domain] || 0;
    var now = Date.now();
    var wait = Math.max(0, MIN_INTERVAL - (now - last));
    if (wait > 0) {
        return new Promise(function (resolve) { setTimeout(resolve, wait); });
    }
    return Promise.resolve();
}

/**
 * 解析 Set-Cookie 头
 */
function parseCookies(setCookieHeaders, domain) {
    if (!setCookieHeaders) return;
    var jar = cookieJars[domain] || {};
    var headers = Array.isArray(setCookieHeaders) ? setCookieHeaders : [setCookieHeaders];
    headers.forEach(function (header) {
        var parts = header.split(';')[0].split('=');
        if (parts.length >= 2) {
            jar[parts[0].trim()] = parts.slice(1).join('=').trim();
        }
    });
    cookieJars[domain] = jar;
}

/**
 * 构建 Cookie 头
 */
function buildCookieHeader(domain) {
    var jar = cookieJars[domain];
    if (!jar) return '';
    return Object.keys(jar).map(function (k) { return k + '=' + jar[k]; }).join('; ');
}

/**
 * 设置指定域名的 Cookie
 */
function setCookies(domain, cookies) {
    if (!cookieJars[domain]) {
        cookieJars[domain] = {};
    }
    Object.keys(cookies).forEach(function (k) {
        cookieJars[domain][k] = cookies[k];
    });
}

/**
 * 发送 HTTP 请求（带重试、限流、Cookie 管理）
 *
 * @param {Object} opts
 * @param {string} opts.url - 请求 URL
 * @param {string} [opts.method='GET'] - HTTP 方法
 * @param {Object} [opts.headers] - 自定义请求头
 * @param {Object|string} [opts.data] - POST 请求体
 * @param {string} [opts.referer] - Referer
 * @param {number} [opts.timeout=15000] - 超时(ms)
 * @param {number} [opts.retries=2] - 重试次数
 * @param {string} [opts.responseType='json'] - 响应类型
 * @returns {Promise<Object>} - { status, data, headers }
 */
async function request(opts) {
    var urlStr = opts.url;
    var method = (opts.method || 'GET').toUpperCase();
    var domain = getDomain(urlStr);
    var maxRetries = opts.retries !== undefined ? opts.retries : 2;

    for (var attempt = 0; attempt <= maxRetries; attempt++) {
        try {
            // 限流
            await waitThrottle(domain);
            lastRequestTime[domain] = Date.now();

            // 构建请求头
            var headers = Object.assign({
                'User-Agent': randomUA(),
                'Accept': '*/*',
                'Accept-Language': 'zh-CN,zh;q=0.9,en;q=0.8'
            }, opts.headers || {});

            // Cookie
            var cookie = buildCookieHeader(domain);
            if (cookie) {
                headers['Cookie'] = cookie;
            }

            // Referer
            if (opts.referer) {
                headers['Referer'] = opts.referer;
            }

            var axiosConfig = {
                url: urlStr,
                method: method,
                headers: headers,
                timeout: opts.timeout || 15000,
                responseType: opts.responseType || 'json',
                validateStatus: function () { return true; } // 不抛异常
            };

            if (method === 'POST' && opts.data) {
                axiosConfig.data = opts.data;
                if (typeof opts.data === 'string') {
                    if (!headers['Content-Type']) {
                        headers['Content-Type'] = 'application/x-www-form-urlencoded';
                    }
                } else if (!headers['Content-Type']) {
                    headers['Content-Type'] = 'application/json';
                }
            }

            var response = await axios(axiosConfig);

            // 保存 Cookie
            if (response.headers && response.headers['set-cookie']) {
                parseCookies(response.headers['set-cookie'], domain);
            }

            // 429 / 503 → 重试
            if ((response.status === 429 || response.status === 503) && attempt < maxRetries) {
                var wait = Math.pow(2, attempt + 1) * 1000;
                console.log('[http] ' + response.status + ' from ' + domain + ', 等待 ' + wait + 'ms 后重试');
                await new Promise(function (resolve) { setTimeout(resolve, wait); });
                continue;
            }

            return {
                status: response.status,
                data: response.data,
                headers: response.headers
            };
        } catch (err) {
            if (attempt < maxRetries) {
                var retryWait = Math.pow(2, attempt) * 1000;
                console.log('[http] 请求失败: ' + err.message + ', 等待 ' + retryWait + 'ms 后重试');
                await new Promise(function (resolve) { setTimeout(resolve, retryWait); });
                continue;
            }
            throw err;
        }
    }
}

/**
 * 验证音频 URL 是否为真实音频（非反爬/引导音频）
 * 通过 HEAD 请求检查 Content-Length，反爬音频通常 < 500KB，
 * 真实歌曲 128kbps 2分钟约 2MB
 *
 * @param {string} audioUrl - 音频 URL
 * @param {number} [minSize] - 最小文件大小(字节)，默认 500KB
 * @returns {Promise<boolean>} - true=有效或无法判断, false=疑似反爬
 */
async function validateAudioUrl(audioUrl, minSize) {
    if (!audioUrl) return false;
    minSize = minSize || 500 * 1024; // 500KB

    try {
        var resp = await axios({
            url: audioUrl,
            method: 'HEAD',
            timeout: 5000,
            headers: {
                'User-Agent': randomUA()
            },
            validateStatus: function () { return true; }
        });

        if (resp.status < 200 || resp.status >= 400) {
            console.log('[validate] HEAD 返回 ' + resp.status + '，无法验证');
            return true; // 无法判断，放行让客户端尝试
        }

        var contentLength = parseInt(resp.headers['content-length'], 10);
        if (!contentLength || isNaN(contentLength)) {
            // 没有 Content-Length（chunked 传输等），无法判断
            return true;
        }

        if (contentLength < minSize) {
            console.log('[validate] 音频文件过小: ' + Math.round(contentLength / 1024) + 'KB (阈值 ' + Math.round(minSize / 1024) + 'KB)，疑似反爬音频');
            return false;
        }

        console.log('[validate] 音频大小正常: ' + Math.round(contentLength / 1024) + 'KB');
        return true;
    } catch (e) {
        // 验证失败不阻断流程
        console.log('[validate] URL 检查异常:', e.message);
        return true;
    }
}

module.exports = {
    request: request,
    setCookies: setCookies,
    randomUA: randomUA,
    validateAudioUrl: validateAudioUrl
};
