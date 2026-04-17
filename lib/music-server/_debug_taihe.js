var http = require('./utils/http');
var crypto = require('./utils/crypto');
function signParams(p) {
    p.timestamp = String(Math.round(Date.now() / 1000));
    p.appid = '16073360';
    var keys = Object.keys(p).sort();
    var pairs = [];
    for (var i = 0; i < keys.length; i++) pairs.push(keys[i] + '=' + p[keys[i]]);
    p.sign = crypto.md5(pairs.join('&') + '0b50b02fd0d73a9c4c8c3a781c30845f');
    return p;
}
var params = signParams({ TSID: 'T10064814019' });
var qs = Object.keys(params).map(function(k) { return encodeURIComponent(k) + '=' + encodeURIComponent(params[k]); }).join('&');
// 先搜索找到免费歌曲
var taihe = require('./providers/taihe');
taihe.search('两只老虎', 10).then(function(results) {
    console.log('搜索结果:', results.length);
    for (var i = 0; i < Math.min(results.length, 5); i++) {
        console.log(' ', results[i].name, '-', results[i].artist, 'id:', results[i].id, 'vip:', results[i].needVip);
    }
    if (results.length > 0) {
        // 依次尝试获取URL
        return tryAll(results, 0);
    }
}).catch(function(e) { console.error('ERR:', e.message); });

function tryAll(results, idx) {
    if (idx >= results.length) {
        console.log('所有歌曲都无法获取URL');
        return;
    }
    var params = signParams({ TSID: results[idx].id });
    var qs = Object.keys(params).map(function(k) { return encodeURIComponent(k) + '=' + encodeURIComponent(params[k]); }).join('&');
    return http.request({ url: 'https://music.taihe.com/v1/song/tracklink?' + qs, method: 'GET', referer: 'https://music.taihe.com/', timeout: 10000 }).then(function(r) {
        var d = r.data && r.data.data;
        if (d) {
            console.log('\n[' + idx + '] ' + (d.title || '') + ' | path:', d.path ? d.path.substring(0, 60) : 'MISSING', '| isVip:', d.isVip, '| pay_model:', d.pay_model);
        }
        if (d && d.path) {
            console.log('找到可播放歌曲！');
            return;
        }
        return tryAll(results, idx + 1);
    });
}
