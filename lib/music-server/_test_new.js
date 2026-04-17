// 临时测试: B站 + 千千音乐
var bilibili = require('./providers/bilibili');
var taihe = require('./providers/taihe');

async function test() {
    // === B站搜索 ===
    console.log('=== Bilibili ===');
    try {
        var biResults = await bilibili.search('牧马城市', 5);
        console.log('搜索结果:', biResults.length);
        if (biResults.length > 0) {
            console.log('  首条:', biResults[0].name, '-', biResults[0].artist, 'id:', biResults[0].id);
            var biUrl = await bilibili.getSongUrl(biResults[0].id, '128');
            console.log('  URL:', biUrl ? biUrl.url.substring(0, 80) : 'NULL');
        }
    } catch (e) { console.error('Bilibili ERR:', e.message); }

    // === 千千音乐搜索 ===
    console.log('\n=== Taihe ===');
    try {
        var thResults = await taihe.search('牧马城市', 5);
        console.log('搜索结果:', thResults.length);
        if (thResults.length > 0) {
            console.log('  首条:', thResults[0].name, '-', thResults[0].artist, 'id:', thResults[0].id);
            var thUrl = await taihe.getSongUrl(thResults[0].id, '128');
            console.log('  URL:', thUrl ? thUrl.url.substring(0, 80) : 'NULL');
        }
    } catch (e) { console.error('Taihe ERR:', e.message); }

    // === B站免费歌曲 ===
    console.log('\n=== Bilibili (小苹果) ===');
    try {
        var bi2 = await bilibili.search('小苹果', 3);
        if (bi2.length > 0) {
            console.log('  首条:', bi2[0].name, '-', bi2[0].artist);
            var biUrl2 = await bilibili.getSongUrl(bi2[0].id, '128');
            console.log('  URL:', biUrl2 ? biUrl2.url.substring(0, 80) : 'NULL');
        }
    } catch (e) { console.error('Bilibili2 ERR:', e.message); }

    // === 千千免费歌曲 ===
    console.log('\n=== Taihe (小苹果) ===');
    try {
        var th2 = await taihe.search('小苹果', 3);
        if (th2.length > 0) {
            console.log('  首条:', th2[0].name, '-', th2[0].artist, 'id:', th2[0].id);
            var thUrl2 = await taihe.getSongUrl(th2[0].id, '128');
            console.log('  URL:', thUrl2 ? thUrl2.url.substring(0, 80) : 'NULL');
        }
    } catch (e) { console.error('Taihe2 ERR:', e.message); }
}

test().then(function() {
    console.log('\n=== 测试完成 ===');
    process.exit(0);
}).catch(function(e) {
    console.error('Fatal:', e);
    process.exit(1);
});
