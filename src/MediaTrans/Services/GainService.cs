using System;

namespace MediaTrans.Services
{
    /// <summary>
    /// 音量增益服务
    /// 提供 dB 值与线性增益的转换，以及 PCM 数据增益应用
    /// </summary>
    public class GainService
    {
        /// <summary>
        /// 最小增益值（dB）
        /// </summary>
        public const double MinGainDb = -20.0;

        /// <summary>
        /// 最大增益值（dB）
        /// </summary>
        public const double MaxGainDb = 20.0;

        /// <summary>
        /// 增益步进值（dB）
        /// </summary>
        public const double GainStepDb = 0.5;

        /// <summary>
        /// 将 dB 值转为线性增益系数
        /// </summary>
        /// <param name="db">增益值（dB）</param>
        /// <returns>线性增益系数</returns>
        public static double DbToLinear(double db)
        {
            return Math.Pow(10.0, db / 20.0);
        }

        /// <summary>
        /// 将线性增益系数转为 dB 值
        /// </summary>
        /// <param name="linear">线性增益系数</param>
        /// <returns>dB 值</returns>
        public static double LinearToDb(double linear)
        {
            if (linear <= 0)
            {
                return MinGainDb;
            }
            return 20.0 * Math.Log10(linear);
        }

        /// <summary>
        /// 钳位增益值到有效范围
        /// </summary>
        /// <param name="gainDb">增益值（dB）</param>
        /// <returns>钳位后的值</returns>
        public static double ClampGainDb(double gainDb)
        {
            if (gainDb < MinGainDb) return MinGainDb;
            if (gainDb > MaxGainDb) return MaxGainDb;
            return gainDb;
        }

        /// <summary>
        /// 将增益值对齐到步进单位
        /// </summary>
        /// <param name="gainDb">增益值（dB）</param>
        /// <returns>对齐后的值</returns>
        public static double SnapToStep(double gainDb)
        {
            return Math.Round(gainDb / GainStepDb) * GainStepDb;
        }

        /// <summary>
        /// 将增益应用到 16 位 PCM 采样数据
        /// </summary>
        /// <param name="samples">原始 PCM 数据（16 位有符号整数格式的字节数组）</param>
        /// <param name="gainDb">增益值（dB）</param>
        /// <returns>增益后的 PCM 数据（新数组）</returns>
        public static byte[] ApplyGainToPcm16(byte[] samples, double gainDb)
        {
            if (samples == null)
            {
                throw new ArgumentNullException("samples");
            }

            if (Math.Abs(gainDb) < 0.001)
            {
                // 0dB 无变化，返回副本
                var copy = new byte[samples.Length];
                Buffer.BlockCopy(samples, 0, copy, 0, samples.Length);
                return copy;
            }

            double linearGain = DbToLinear(gainDb);
            var result = new byte[samples.Length];

            // 每个采样占 2 字节（16 位）
            for (int i = 0; i < samples.Length - 1; i += 2)
            {
                short sample = (short)(samples[i] | (samples[i + 1] << 8));
                double amplified = sample * linearGain;

                // 钳位防止溢出
                if (amplified > short.MaxValue) amplified = short.MaxValue;
                if (amplified < short.MinValue) amplified = short.MinValue;

                short clampedSample = (short)amplified;
                result[i] = (byte)(clampedSample & 0xFF);
                result[i + 1] = (byte)((clampedSample >> 8) & 0xFF);
            }

            return result;
        }

        /// <summary>
        /// 将增益应用到浮点采样数据
        /// </summary>
        /// <param name="samples">浮点 PCM 采样数据</param>
        /// <param name="gainDb">增益值（dB）</param>
        /// <returns>增益后的采样数据（新数组）</returns>
        public static float[] ApplyGainToFloat(float[] samples, double gainDb)
        {
            if (samples == null)
            {
                throw new ArgumentNullException("samples");
            }

            double linearGain = DbToLinear(gainDb);
            var result = new float[samples.Length];

            for (int i = 0; i < samples.Length; i++)
            {
                double amplified = samples[i] * linearGain;

                // 钳位到 [-1.0, 1.0]
                if (amplified > 1.0) amplified = 1.0;
                if (amplified < -1.0) amplified = -1.0;

                result[i] = (float)amplified;
            }

            return result;
        }

        /// <summary>
        /// 原地应用增益到浮点采样数据（不创建新数组）
        /// </summary>
        /// <param name="samples">浮点 PCM 采样数据（会被修改）</param>
        /// <param name="gainDb">增益值（dB）</param>
        public static void ApplyGainToFloatInPlace(float[] samples, double gainDb)
        {
            if (samples == null)
            {
                return;
            }

            if (Math.Abs(gainDb) < 0.001)
            {
                return; // 0dB 无变化
            }

            double linearGain = DbToLinear(gainDb);

            for (int i = 0; i < samples.Length; i++)
            {
                double amplified = samples[i] * linearGain;

                if (amplified > 1.0) amplified = 1.0;
                if (amplified < -1.0) amplified = -1.0;

                samples[i] = (float)amplified;
            }
        }

        /// <summary>
        /// 格式化增益显示文本
        /// </summary>
        /// <param name="gainDb">增益值（dB）</param>
        /// <returns>格式化文本（如 "+6.0 dB" 或 "-3.5 dB" 或 "0.0 dB"）</returns>
        public static string FormatGainText(double gainDb)
        {
            if (gainDb > 0)
            {
                return string.Format("+{0:F1} dB", gainDb);
            }
            return string.Format("{0:F1} dB", gainDb);
        }
    }
}
