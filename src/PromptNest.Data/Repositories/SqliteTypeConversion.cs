using System.Globalization;

namespace PromptNest.Data.Repositories;

internal static class SqliteTypeConversion
{
    public static long ToUnixTimeMilliseconds(DateTimeOffset value) => value.ToUnixTimeMilliseconds();

    public static DateTimeOffset FromUnixTimeMilliseconds(long value) => DateTimeOffset.FromUnixTimeMilliseconds(value);

    public static DateTimeOffset? FromNullableUnixTimeMilliseconds(long? value) =>
        value is null ? null : DateTimeOffset.FromUnixTimeMilliseconds(value.Value);

    public static int ToSqliteBoolean(bool value) => value ? 1 : 0;

    public static bool FromSqliteBoolean(long value) => value != 0;

    public static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

    public static string ToInvariantString(object value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    public static long ToInt64(object value)
    {
        if (value is byte[] bytes)
        {
            return long.Parse(System.Text.Encoding.UTF8.GetString(bytes), CultureInfo.InvariantCulture);
        }

        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }
}