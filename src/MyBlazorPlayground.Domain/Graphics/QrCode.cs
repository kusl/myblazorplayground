using System.Globalization;
using System.Text;

namespace MyBlazorPlayground.Domain.Graphics;

public sealed class QrCode
{
    public const int MaxVersion = 10;

    private static readonly int[] EccPerBlock = [-1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26];
    private static readonly int[] Blocks = [-1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5];

    private readonly bool[,] _modules;
    private readonly bool[,] _function;

    private QrCode(int version)
    {
        Version = version;
        Size = version * 4 + 17;
        _modules = new bool[Size, Size];
        _function = new bool[Size, Size];
    }

    public int Version { get; }

    public int Size { get; }

    public int Mask { get; private set; }

    public bool this[int x, int y] => x >= 0 && y >= 0 && x < Size && y < Size && _modules[y, x];

    public static int Capacity(int version) => DataCodewords(version) - (version < 10 ? 2 : 3);

    public static QrCode? Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var bytes = Encoding.UTF8.GetBytes(text);
        for (var version = 1; version <= MaxVersion; version++)
        {
            if (bytes.Length <= Capacity(version))
            {
                return Build(version, Codewords(version, bytes));
            }
        }

        return null;
    }

    public string ToSvgPath(int border = 4)
    {
        var path = new StringBuilder();
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (_modules[y, x])
                {
                    path.Append(CultureInfo.InvariantCulture, $"M{x + border},{y + border}h1v1h-1z");
                }
            }
        }

        return path.ToString();
    }

    public string ToSvg(string title, int border = 4)
    {
        ArgumentNullException.ThrowIfNull(title);
        var extent = Size + border * 2;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {extent} {extent}\" role=\"img\" aria-label=\"{System.Net.WebUtility.HtmlEncode(title)}\" shape-rendering=\"crispEdges\"><rect width=\"{extent}\" height=\"{extent}\" fill=\"#fff\"/><path d=\"{ToSvgPath(border)}\" fill=\"#000\"/></svg>");
    }

    private static int RawModules(int version)
    {
        var result = (16 * version + 128) * version + 64;
        if (version >= 2)
        {
            var alignments = version / 7 + 2;
            result -= (25 * alignments - 10) * alignments - 55;
            if (version >= 7)
            {
                result -= 36;
            }
        }

        return result;
    }

    private static int DataCodewords(int version) => RawModules(version) / 8 - EccPerBlock[version] * Blocks[version];

    private static byte[] Codewords(int version, byte[] data)
    {
        var bits = new List<bool>();
        void Append(int value, int length)
        {
            for (var i = length - 1; i >= 0; i--)
            {
                bits.Add(((value >> i) & 1) != 0);
            }
        }

        Append(0b0100, 4);
        Append(data.Length, version < 10 ? 8 : 16);
        foreach (var value in data)
        {
            Append(value, 8);
        }

        var capacity = DataCodewords(version) * 8;
        Append(0, Math.Min(4, capacity - bits.Count));
        Append(0, (8 - bits.Count % 8) % 8);
        for (var pad = 0xEC; bits.Count < capacity; pad ^= 0xEC ^ 0x11)
        {
            Append(pad, 8);
        }

        var result = new byte[bits.Count / 8];
        for (var i = 0; i < bits.Count; i++)
        {
            if (bits[i])
            {
                result[i >> 3] |= (byte)(1 << (7 - (i & 7)));
            }
        }

        return result;
    }

    private static QrCode Build(int version, byte[] data)
    {
        var all = Interleave(version, data);
        QrCode? best = null;
        var bestPenalty = int.MaxValue;
        for (var mask = 0; mask < 8; mask++)
        {
            var code = new QrCode(version);
            code.DrawFunctionPatterns();
            code.DrawCodewords(all);
            code.ApplyMask(mask);
            code.DrawFormatBits(mask);
            code.Mask = mask;
            var penalty = code.Penalty();
            if (penalty < bestPenalty)
            {
                best = code;
                bestPenalty = penalty;
            }
        }

        return best!;
    }

    private static byte[] Interleave(int version, byte[] data)
    {
        var blockCount = Blocks[version];
        var eccLength = EccPerBlock[version];
        var raw = RawModules(version) / 8;
        var shortBlocks = blockCount - raw % blockCount;
        var shortLength = raw / blockCount;
        var divisor = Divisor(eccLength);
        var blocks = new List<byte[]>();
        for (int i = 0, k = 0; i < blockCount; i++)
        {
            var length = shortLength - eccLength + (i < shortBlocks ? 0 : 1);
            var chunk = data.AsSpan(k, length).ToArray();
            k += length;
            var ecc = Remainder(chunk, divisor);
            var block = new byte[shortLength + 1];
            chunk.CopyTo(block, 0);
            ecc.CopyTo(block, block.Length - eccLength);
            blocks.Add(block);
        }

        var result = new List<byte>(raw);
        for (var i = 0; i < blocks[0].Length; i++)
        {
            for (var j = 0; j < blocks.Count; j++)
            {
                if (i != shortLength - eccLength || j >= shortBlocks)
                {
                    result.Add(blocks[j][i]);
                }
            }
        }

        return [.. result];
    }

    private static byte[] Divisor(int degree)
    {
        var result = new byte[degree];
        result[degree - 1] = 1;
        var root = 1;
        for (var i = 0; i < degree; i++)
        {
            for (var j = 0; j < degree; j++)
            {
                result[j] = Multiply(result[j], root);
                if (j + 1 < degree)
                {
                    result[j] ^= result[j + 1];
                }
            }

            root = Multiply(root, 0x02);
        }

        return result;
    }

    private static byte[] Remainder(byte[] data, byte[] divisor)
    {
        var result = new byte[divisor.Length];
        foreach (var value in data)
        {
            var factor = value ^ result[0];
            Array.Copy(result, 1, result, 0, result.Length - 1);
            result[^1] = 0;
            for (var i = 0; i < result.Length; i++)
            {
                result[i] ^= Multiply(divisor[i], factor);
            }
        }

        return result;
    }

    private static byte Multiply(int x, int y)
    {
        var z = 0;
        for (var i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((y >> i) & 1) * x;
        }

        return (byte)z;
    }

    private void Set(int x, int y, bool dark)
    {
        _modules[y, x] = dark;
        _function[y, x] = true;
    }

    private void DrawFunctionPatterns()
    {
        for (var i = 0; i < Size; i++)
        {
            Set(6, i, i % 2 == 0);
            Set(i, 6, i % 2 == 0);
        }

        DrawFinder(3, 3);
        DrawFinder(Size - 4, 3);
        DrawFinder(3, Size - 4);
        var positions = AlignmentPositions();
        for (var i = 0; i < positions.Length; i++)
        {
            for (var j = 0; j < positions.Length; j++)
            {
                if (!((i == 0 && j == 0) || (i == 0 && j == positions.Length - 1) || (i == positions.Length - 1 && j == 0)))
                {
                    DrawAlignment(positions[i], positions[j]);
                }
            }
        }

        DrawFormatBits(0);
        DrawVersion();
    }

    private void DrawFinder(int x, int y)
    {
        for (var dy = -4; dy <= 4; dy++)
        {
            for (var dx = -4; dx <= 4; dx++)
            {
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                if (x + dx >= 0 && x + dx < Size && y + dy >= 0 && y + dy < Size)
                {
                    Set(x + dx, y + dy, distance != 2 && distance != 4);
                }
            }
        }
    }

    private void DrawAlignment(int x, int y)
    {
        for (var dy = -2; dy <= 2; dy++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                Set(x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
            }
        }
    }

    private int[] AlignmentPositions()
    {
        if (Version == 1)
        {
            return [];
        }

        var count = Version / 7 + 2;
        var step = (Version * 4 + count * 2 + 1) / (count * 2 - 2) * 2;
        var result = new int[count];
        result[0] = 6;
        for (int i = count - 1, position = Size - 7; i >= 1; i--, position -= step)
        {
            result[i] = position;
        }

        return result;
    }

    private void DrawFormatBits(int mask)
    {
        var data = mask;
        var remainder = data;
        for (var i = 0; i < 10; i++)
        {
            remainder = (remainder << 1) ^ ((remainder >> 9) * 0x537);
        }

        var bits = ((data << 10) | remainder) ^ 0x5412;
        bool Bit(int index) => ((bits >> index) & 1) != 0;
        for (var i = 0; i <= 5; i++)
        {
            Set(8, i, Bit(i));
        }

        Set(8, 7, Bit(6));
        Set(8, 8, Bit(7));
        Set(7, 8, Bit(8));
        for (var i = 9; i < 15; i++)
        {
            Set(14 - i, 8, Bit(i));
        }

        for (var i = 0; i < 8; i++)
        {
            Set(Size - 1 - i, 8, Bit(i));
        }

        for (var i = 8; i < 15; i++)
        {
            Set(8, Size - 15 + i, Bit(i));
        }

        Set(8, Size - 8, true);
    }

    private void DrawVersion()
    {
        if (Version < 7)
        {
            return;
        }

        var remainder = Version;
        for (var i = 0; i < 12; i++)
        {
            remainder = (remainder << 1) ^ ((remainder >> 11) * 0x1F25);
        }

        var bits = (Version << 12) | remainder;
        for (var i = 0; i < 18; i++)
        {
            var dark = ((bits >> i) & 1) != 0;
            var a = Size - 11 + i % 3;
            var b = i / 3;
            Set(a, b, dark);
            Set(b, a, dark);
        }
    }

    private void DrawCodewords(byte[] data)
    {
        var i = 0;
        for (var right = Size - 1; right >= 1; right -= 2)
        {
            if (right == 6)
            {
                right = 5;
            }

            for (var vertical = 0; vertical < Size; vertical++)
            {
                for (var j = 0; j < 2; j++)
                {
                    var x = right - j;
                    var upward = ((right + 1) & 2) == 0;
                    var y = upward ? Size - 1 - vertical : vertical;
                    if (!_function[y, x] && i < data.Length * 8)
                    {
                        _modules[y, x] = ((data[i >> 3] >> (7 - (i & 7))) & 1) != 0;
                        i++;
                    }
                }
            }
        }
    }

    private void ApplyMask(int mask)
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var invert = mask switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (x + y) % 3 == 0,
                    4 => (x / 3 + y / 2) % 2 == 0,
                    5 => x * y % 2 + x * y % 3 == 0,
                    6 => (x * y % 2 + x * y % 3) % 2 == 0,
                    _ => ((x + y) % 2 + x * y % 3) % 2 == 0,
                };
                _modules[y, x] ^= invert && !_function[y, x];
            }
        }
    }

    private int Penalty()
    {
        var penalty = 0;
        for (var line = 0; line < Size; line++)
        {
            penalty += Runs(index => _modules[line, index]) + Runs(index => _modules[index, line]);
        }

        for (var y = 0; y < Size - 1; y++)
        {
            for (var x = 0; x < Size - 1; x++)
            {
                var color = _modules[y, x];
                if (color == _modules[y, x + 1] && color == _modules[y + 1, x] && color == _modules[y + 1, x + 1])
                {
                    penalty += 3;
                }
            }
        }

        var dark = 0;
        foreach (var module in _modules)
        {
            dark += module ? 1 : 0;
        }

        var total = Size * Size;
        penalty += (Math.Abs(dark * 20 - total * 10) + total - 1) / total * 10 - 10;
        return penalty;
    }

    private int Runs(Func<int, bool> at)
    {
        var penalty = 0;
        var run = 1;
        for (var i = 1; i <= Size; i++)
        {
            if (i < Size && at(i) == at(i - 1))
            {
                run++;
                continue;
            }

            if (run >= 5)
            {
                penalty += run - 2;
            }

            run = 1;
        }

        return penalty;
    }
}
