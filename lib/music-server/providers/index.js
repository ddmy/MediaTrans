'use strict';

/**
 * Provider 注册表
 */

var netease = require('./netease');
var qq = require('./qq');
var kugou = require('./kugou');
var kuwo = require('./kuwo');
var migu = require('./migu');
var bilibili = require('./bilibili');
var taihe = require('./taihe');

var providerMap = {};
var providerList = [];

function register(provider) {
    providerMap[provider.name] = provider;
    providerList.push(provider);
}

register(netease);
register(qq);
register(kugou);
register(kuwo);
register(migu);
register(bilibili);
register(taihe);

module.exports = {
    /**
     * 获取指定平台的 Provider
     */
    get: function (name) {
        return providerMap[name] || null;
    },

    /**
     * 获取所有 Provider 列表
     */
    list: function () {
        return providerList;
    },

    /**
     * 获取指定平台列表的 Provider
     */
    getByNames: function (names) {
        if (!names || names.length === 0) return providerList;
        return names.map(function (n) { return providerMap[n]; }).filter(Boolean);
    }
};
