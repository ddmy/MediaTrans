var http = require('./utils/http');

async function debug() {
    // Debug Bilibili search
    console.log('=== Debug Bilibili ===');
    try {
        var resp = await http.request({
            url: 'https://api.bilibili.com/x/web-interface/search/type?search_type=video&keyword=' + encodeURIComponent('牧马城市') + '&page=1&page_size=5',
            method: 'GET',
            headers: {
                'Cookie': 'buvid3=0',
                'Referer': 'https://search.bilibili.com/'
            },
            timeout: 10000
        });
        console.log('Status:', resp.status);
        console.log('Code:', resp.data ? resp.data.code : 'N/A');
        console.log('Message:', resp.data ? resp.data.message : 'N/A');
        if (resp.data && resp.data.data && resp.data.data.result) {
            console.log('Results:', resp.data.data.result.length);
            if (resp.data.data.result[0]) {
                var item = resp.data.data.result[0];
                console.log('First:', item.title, '|', item.author, '|', item.bvid, '| dur:', item.duration);
            }
        } else {
            console.log('Data keys:', resp.data && resp.data.data ? Object.keys(resp.data.data) : 'no data');
        }
    } catch (e) { console.error('Bilibili ERR:', e.message); }

    // Debug Taihe search
    console.log('\n=== Debug Taihe ===');
    try {
        var cryptoUtil = require('./utils/crypto');
        var params = {
            word: '牧马城市',
            pageNo: '1',
            type: '1'
        };
        params.timestamp = String(Math.round(Date.now() / 1000));
        params.appid = '16073360';
        var keys = Object.keys(params).sort();
        var pairs = [];
        for (var i = 0; i < keys.length; i++) {
            pairs.push(keys[i] + '=' + params[keys[i]]);
        }
        var signStr = pairs.join('&') + '0b50b02fd0d73a9c4c8c3a781c30845f';
        params.sign = cryptoUtil.md5(signStr);
        
        var qs = Object.keys(params).map(function(k) {
            return encodeURIComponent(k) + '=' + encodeURIComponent(params[k]);
        }).join('&');
        
        console.log('URL: https://music.taihe.com/v1/search?' + qs);
        
        var resp2 = await http.request({
            url: 'https://music.taihe.com/v1/search?' + qs,
            method: 'GET',
            referer: 'https://music.taihe.com/',
            timeout: 10000
        });
        console.log('Status:', resp2.status);
        console.log('Code:', resp2.data ? resp2.data.code : 'N/A');
        console.log('Msg:', resp2.data ? resp2.data.msg : 'N/A');
        if (resp2.data && resp2.data.data) {
            console.log('Data keys:', Object.keys(resp2.data.data));
            if (resp2.data.data.typeTrack) {
                console.log('Tracks:', resp2.data.data.typeTrack.length);
                if (resp2.data.data.typeTrack[0]) {
                    var t = resp2.data.data.typeTrack[0];
                    console.log('First:', t.title, '|', t.artist ? t.artist[0].name : 'unknown', '| id:', t.id);
                }
            }
        } else {
            console.log('Raw data:', JSON.stringify(resp2.data).substring(0, 500));
        }
    } catch (e) { console.error('Taihe ERR:', e.message, e.stack ? e.stack.split('\n')[1] : ''); }
}

debug().then(function() { process.exit(0); });
