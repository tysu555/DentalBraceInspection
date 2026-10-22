using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DentalBraceInspection.Models;

namespace DentalBraceInspection.Converters
{
    /// <summary>日志级别 -> 颜色。</summary>
    public sealed class LogLevelToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Info = new SolidColorBrush(Color.FromRgb(0x8A, 0x97, 0xA6));
        private static readonly SolidColorBrush Success = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
        private static readonly SolidColorBrush Warning = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
        private static readonly SolidColorBrush Error = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));

        static LogLevelToBrushConverter()
        {
            Info.Freeze(); Success.Freeze(); Warning.Freeze(); Error.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is LogLevel level)) return Info;
            switch (level)
            {
                case LogLevel.Success: return Success;
                case LogLevel.Warning: return Warning;
                case LogLevel.Error: return Error;
                default: return Info;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
