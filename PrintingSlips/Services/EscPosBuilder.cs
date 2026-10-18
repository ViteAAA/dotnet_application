using System.Text;

namespace PrintingSlips.Services;

public class EscPosBuilder
{
    private readonly List<byte> _buffer = new();
    private readonly Encoding _encoding;
    private readonly int _charsPerLine;

    // Регистрация провайдера кодировок (нужно для CP866/1251 в .NET Core)
    static EscPosBuilder()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public EscPosBuilder(int charsPerLine, string encodingName = "cp866")
    {
        _charsPerLine = charsPerLine;
        _encoding = Encoding.GetEncoding(encodingName);
    }

    public EscPosBuilder Initialize(byte codePage = 17) // 17 обычно это CP866
    {
        _buffer.AddRange([0x1B, 0x40]);       // ESC @ (Сброс настроек)
        _buffer.AddRange([0x1C, 0x2E]);       // FS .  (ОТКЛЮЧЕНИЕ КИТАЙСКОГО РЕЖИМА - решает проблему иероглифов)
        _buffer.AddRange([0x1B, 0x74, codePage]); // ESC t <n> (Выбор кодовой страницы)
        return this;
    }

    public EscPosBuilder AlignLeft() => AppendCmd([0x1B, 0x61, 0x00]);
    public EscPosBuilder AlignCenter() => AppendCmd([0x1B, 0x61, 0x01]);
    public EscPosBuilder BoldOn() => AppendCmd([0x1B, 0x45, 0x01]);
    public EscPosBuilder BoldOff() => AppendCmd([0x1B, 0x45, 0x00]);
    public EscPosBuilder CutPaper() => AppendCmd([0x1D, 0x56, 0x42, 0x00]);

    public EscPosBuilder SetFontSize(int size)
    {
        var mode = Math.Clamp(size, 1, 3) switch { 2 => 0x10, 3 => 0x30, _ => 0x00 };
        return AppendCmd([0x1B, 0x21, (byte)mode]);
    }

    public EscPosBuilder PrintLine(string? text = "")
    {
        if (string.IsNullOrEmpty(text))
        {
            _buffer.Add((byte)'\n');
            return this;
        }

        // Корректный перенос по словам, а не обрезка на полуслове
        foreach (var line in WordWrap(text, _charsPerLine))
        {
            _buffer.AddRange(_encoding.GetBytes(line));
            _buffer.Add((byte)'\n');
        }
        return this;
    }

    public EscPosBuilder PrintSeparator(char symbol = '-')
    {
        _buffer.AddRange(_encoding.GetBytes(new string(symbol, _charsPerLine)));
        _buffer.Add((byte)'\n');
        return this;
    }

    public EscPosBuilder FeedLines(int count)
    {
        for (int i = 0; i < count; i++) _buffer.Add((byte)'\n');
        return this;
    }

    public byte[] Build() => [.. _buffer];

    private EscPosBuilder AppendCmd(byte[] cmd)
    {
        _buffer.AddRange(cmd);
        return this;
    }

    // Корректный алгоритм переноса строк по границе слова
    private static IEnumerable<string> WordWrap(string text, int maxWidth)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentLine = string.Empty;

        foreach (var word in words)
        {
            if (currentLine.Length + word.Length + 1 > maxWidth)
            {
                if (currentLine.Length > 0)
                {
                    yield return currentLine;
                    currentLine = string.Empty;
                }
                
                if (word.Length > maxWidth) // Если одно слово длиннее строки
                {
                    yield return word[..maxWidth];
                    currentLine = word[maxWidth..];
                }
                else
                {
                    currentLine = word;
                }
            }
            else
            {
                currentLine += (currentLine.Length > 0 ? " " : "") + word;
            }
        }
        if (currentLine.Length > 0) yield return currentLine;
    }
}