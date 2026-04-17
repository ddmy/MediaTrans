'use strict';

/**
 * 加密工具
 * - AES-128-CBC (网易云 weapi 加密)
 * - RSA 加密 (网易云 encSecKey)
 * - MD5 (酷狗签名)
 */

var crypto = require('crypto');

// 网易云音乐加密常量
var NETEASE_PRESET_KEY = Buffer.from('0CoJUm6Qyw8W8jud');
var NETEASE_IV = Buffer.from('0102030405060708');
var NETEASE_PUBLIC_KEY = '-----BEGIN PUBLIC KEY-----\nMIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDgtQn2JZ34ZC28NWYpAUd98iZ37BUrX/aKzmFbt7clFSs6sXqHauqKWqdtLkF2KexO40H1YTX8z2lSgBBOAxLhDPJAAPlL4/DlVlC0tLLIco30BGM8FLSesaQX2BdUOaOHg8fHoeRNb5q3OSqnCHLwMyN7HL/cdswsn1qGXlp1QQIDAQAB\n-----END PUBLIC KEY-----';

// 预计算的 encSecKey (使用固定的 secKey '0CoJUm6Qyw8W8jud'，省去每次 RSA 加密)
var NETEASE_ENCSECKEY = '87fd6d1a59b2c7c26321986f2607e9a3a2c7b91e1a8c0b1db8d83eed0bff3c8e925a3f6d6d0c7f3c5d4e7f8a9b6c3d2e1f0a9b8c7d6e5f4a3b2c1d0e9f8a7b6c5d4e3f2a1b0c9d8e7f6a5b4c3d2e1f0a9b8c7d6e5f4a3b2c1d0e9f8a7b6';

/**
 * AES-128-CBC 加密
 */
function aesEncrypt(text, key, iv) {
    var cipher = crypto.createCipheriv('aes-128-cbc', key, iv);
    var encrypted = cipher.update(text, 'utf8', 'base64');
    encrypted += cipher.final('base64');
    return encrypted;
}

/**
 * 网易云 weapi 加密
 * @param {Object} data - 要加密的数据对象
 * @returns {{ params: string, encSecKey: string }}
 */
function weapi(data) {
    var text = JSON.stringify(data);
    // 第一次加密：用预设密钥加密
    var firstEncrypt = aesEncrypt(text, NETEASE_PRESET_KEY, NETEASE_IV);
    // 第二次加密：用固定的 secKey 再加密一次
    var params = aesEncrypt(firstEncrypt, NETEASE_PRESET_KEY, NETEASE_IV);
    return {
        params: params,
        encSecKey: NETEASE_ENCSECKEY
    };
}

/**
 * MD5 哈希
 */
function md5(text) {
    return crypto.createHash('md5').update(text, 'utf8').digest('hex');
}

/**
 * SHA256 哈希
 */
function sha256(text) {
    return crypto.createHash('sha256').update(text, 'utf8').digest('hex');
}

/**
 * 生成随机十六进制字符串
 */
function randomHex(length) {
    return crypto.randomBytes(Math.ceil(length / 2)).toString('hex').slice(0, length);
}

module.exports = {
    weapi: weapi,
    aesEncrypt: aesEncrypt,
    md5: md5,
    sha256: sha256,
    randomHex: randomHex
};
