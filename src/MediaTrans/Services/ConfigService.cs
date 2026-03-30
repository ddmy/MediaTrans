using System;
using System.IO;
using System.Text;
using MediaTrans.Models;
using Newtonsoft.Json;

namespace MediaTrans.Services
{
    /// <summary>
    /// 配置服务，负责 AppConfig.json 的读写
    /// </summary>
    public class ConfigService
    {
        private readonly string _configPath;
        private AppConfig _currentConfig;

        /// <summary>
        /// 当前配置
        /// </summary>
        public AppConfig CurrentConfig
        {
            get { return _currentConfig; }
        }

        /// <summary>
        /// 创建配置服务实例
        /// </summary>
        /// <param name="configPath">配置文件路径，为空时使用默认路径</param>
        public ConfigService(string configPath = null)
        {
            if (string.IsNullOrEmpty(configPath))
            {
                // 默认使用应用程序目录下的 Config/AppConfig.json
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                _configPath = Path.Combine(appDir, "Config", "AppConfig.json");
            }
            else
            {
                _configPath = configPath;
            }
        }

        /// <summary>
        /// 加载配置文件，不存在时生成默认配置
        /// </summary>
        /// <returns>加载的配置对象</returns>
        public AppConfig Load()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    string json = File.ReadAllText(_configPath, Encoding.UTF8);
                    _currentConfig = JsonConvert.DeserializeObject<AppConfig>(json);
                    if (_currentConfig == null)
                    {
                        _currentConfig = AppConfig.CreateDefault();
                        Save(_currentConfig);
                    }
                }
                catch (Exception)
                {
                    // 配置文件损坏，使用默认配置并覆盖
                    _currentConfig = AppConfig.CreateDefault();
                    Save(_currentConfig);
                }
            }
            else
            {
                // 配置文件不存在，生成默认配置
                _currentConfig = AppConfig.CreateDefault();
                Save(_currentConfig);
            }

            return _currentConfig;
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        /// <param name="config">要保存的配置对象</param>
        public void Save(AppConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            _currentConfig = config;

            string dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(_configPath, json, Encoding.UTF8);
        }

        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        public string ConfigPath
        {
            get { return _configPath; }
        }
    }
}
