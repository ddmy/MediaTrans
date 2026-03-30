using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace MediaTrans.Services
{
    /// <summary>
    /// 机器码采集服务
    /// 通过 WMI 采集 CPU ID + 硬盘序列号 + 主板序列号，SHA256 哈希生成唯一机器码
    /// </summary>
    public class MachineCodeService
    {
        /// <summary>
        /// 获取当前机器的唯一机器码（SHA256 哈希，大写十六进制）
        /// </summary>
        /// <returns>64 字符的十六进制机器码</returns>
        public string GetMachineCode()
        {
            string cpuId = GetWmiProperty("Win32_Processor", "ProcessorId");
            string diskSerial = GetWmiProperty("Win32_DiskDrive", "SerialNumber");
            string boardSerial = GetWmiProperty("Win32_BaseBoard", "SerialNumber");

            // 组合硬件信息生成唯一标识
            string raw = string.Format("CPU:{0}|DISK:{1}|BOARD:{2}", 
                cpuId ?? "", diskSerial ?? "", boardSerial ?? "");

            return ComputeSha256(raw);
        }

        /// <summary>
        /// 通过指定的硬件信息字符串生成机器码（用于测试）
        /// </summary>
        /// <param name="cpuId">CPU ID</param>
        /// <param name="diskSerial">硬盘序列号</param>
        /// <param name="boardSerial">主板序列号</param>
        /// <returns>64 字符的十六进制机器码</returns>
        public string GenerateMachineCode(string cpuId, string diskSerial, string boardSerial)
        {
            string raw = string.Format("CPU:{0}|DISK:{1}|BOARD:{2}",
                cpuId ?? "", diskSerial ?? "", boardSerial ?? "");

            return ComputeSha256(raw);
        }

        /// <summary>
        /// 获取 CPU ID
        /// </summary>
        public string GetCpuId()
        {
            return GetWmiProperty("Win32_Processor", "ProcessorId");
        }

        /// <summary>
        /// 获取硬盘序列号
        /// </summary>
        public string GetDiskSerial()
        {
            return GetWmiProperty("Win32_DiskDrive", "SerialNumber");
        }

        /// <summary>
        /// 获取主板序列号
        /// </summary>
        public string GetBoardSerial()
        {
            return GetWmiProperty("Win32_BaseBoard", "SerialNumber");
        }

        /// <summary>
        /// 通过 WMI 查询指定类的指定属性值
        /// </summary>
        /// <param name="wmiClass">WMI 类名</param>
        /// <param name="propertyName">属性名</param>
        /// <returns>属性值字符串，失败返回空字符串</returns>
        private string GetWmiProperty(string wmiClass, string propertyName)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    string.Format("SELECT {0} FROM {1}", propertyName, wmiClass)))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        object value = obj[propertyName];
                        if (value != null)
                        {
                            string result = value.ToString().Trim();
                            if (!string.IsNullOrEmpty(result))
                            {
                                return result;
                            }
                        }
                    }
                }
            }
            catch
            {
                // WMI 查询失败时返回空字符串，确保程序不崩溃
            }
            return "";
        }

        /// <summary>
        /// 计算 SHA256 哈希值
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <returns>大写十六进制哈希值</returns>
        private string ComputeSha256(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = sha256.ComputeHash(inputBytes);

                var sb = new StringBuilder(64);
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }
    }
}
