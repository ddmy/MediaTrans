using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MediaTrans.Converters
{
    /// <summary>
    /// 播放头/标记线宽度（像素）
    /// </summary>
    internal static class WaveformConstants
    {
        public const double MarkerWidth = 2.0;
    }

    /// <summary>
    /// 将播放进度百分比（0‑100）和容器宽度转换为左边距 Thickness，用于在波形上定位播放头
    /// values[0] = double progress (0‑100), values[1] = double containerWidth
    /// </summary>
    public class ProgressToLeftMarginConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double percent = 0;
            double width = 0;
            if (values != null && values.Length >= 2)
            {
                if (values[0] is double) percent = (double)values[0];
                if (values[1] is double) width = (double)values[1];
            }
            double x = percent / 100.0 * width;
            if (x < 0) x = 0;
            if (width > WaveformConstants.MarkerWidth && x > width - WaveformConstants.MarkerWidth)
                x = width - WaveformConstants.MarkerWidth;
            return new Thickness(x, 0, 0, 0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[] { DependencyProperty.UnsetValue, DependencyProperty.UnsetValue };
        }
    }

    /// <summary>
    /// 将裁剪起点百分比、终点百分比和容器宽度转换为裁剪区域 Thickness（Left=起点偏移, Right=终点右侧余量）
    /// values[0] = double startPercent (0‑100), values[1] = double endPercent (0‑100), values[2] = double containerWidth
    /// </summary>
    public class TrimRegionMarginConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double startPct = 0;
            double endPct = 100;
            double width = 0;
            if (values != null && values.Length >= 3)
            {
                if (values[0] is double) startPct = (double)values[0];
                if (values[1] is double) endPct = (double)values[1];
                if (values[2] is double) width = (double)values[2];
            }
            double left = startPct / 100.0 * width;
            double right = (1.0 - endPct / 100.0) * width;
            if (left < 0) left = 0;
            if (right < 0) right = 0;
            return new Thickness(left, 0, right, 0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[] { DependencyProperty.UnsetValue, DependencyProperty.UnsetValue, DependencyProperty.UnsetValue };
        }
    }


    /// <summary>
    /// 布尔值转 Visibility 转换器
    /// true => Visible, false => Collapsed
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool && (bool)value)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility && (Visibility)value == Visibility.Visible;
        }
    }

    /// <summary>
    /// 布尔值取反转 Visibility 转换器
    /// true => Collapsed, false => Visible
    /// </summary>
    public class BoolToVisibilityInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool && (bool)value)
            {
                return Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility && (Visibility)value == Visibility.Collapsed;
        }
    }
}
