using System.Text;

namespace Quassel.Client.Application.Text;

public static class IrcFormattingCleaner
{
    public static string Clean(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(input.Length);

        for (var index = 0; index < input.Length; index++)
        {
            var current = input[index];
            switch (current)
            {
                case '\x02':
                case '\x0F':
                case '\x11':
                case '\x16':
                case '\x1D':
                case '\x1E':
                case '\x1F':
                    break;
                case '\x03':
                    index = SkipMircColorParameters(input, index);
                    break;
                case '\x04':
                    index = SkipHexColorParameters(input, index);
                    break;
                default:
                    if (char.IsControl(current) && current is not '\r' and not '\n' and not '\t')
                    {
                        break;
                    }

                    builder.Append(current);
                    break;
            }
        }

        return builder.ToString();
    }

    private static int SkipMircColorParameters(string input, int currentIndex)
    {
        var index = currentIndex + 1;
        index = SkipDigits(input, index, 2);

        if (index < input.Length && input[index] == ',')
        {
            index++;
            index = SkipDigits(input, index, 2);
        }

        return index - 1;
    }

    private static int SkipHexColorParameters(string input, int currentIndex)
    {
        var index = currentIndex + 1;
        index = SkipHexDigits(input, index, 6);

        if (index < input.Length && input[index] == ',')
        {
            index++;
            index = SkipHexDigits(input, index, 6);
        }

        return index - 1;
    }

    private static int SkipDigits(string input, int startIndex, int maxDigits)
    {
        var index = startIndex;
        var count = 0;

        while (index < input.Length && count < maxDigits && char.IsAsciiDigit(input[index]))
        {
            index++;
            count++;
        }

        return index;
    }

    private static int SkipHexDigits(string input, int startIndex, int maxDigits)
    {
        var index = startIndex;
        var count = 0;

        while (index < input.Length && count < maxDigits && IsHexDigit(input[index]))
        {
            index++;
            count++;
        }

        return index;
    }

    private static bool IsHexDigit(char value)
    {
        return value is >= '0' and <= '9'
            or >= 'A' and <= 'F'
            or >= 'a' and <= 'f';
    }
}
