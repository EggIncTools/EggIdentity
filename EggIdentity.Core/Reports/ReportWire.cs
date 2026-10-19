using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using EggIdentity.Filters;

namespace EggIdentity.Reports;

public sealed record CubeWire(
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<MeasureRequest> Measures,
    bool Normalized,
    int Rows,
    IReadOnlyList<IReadOnlyList<string>> Keys,
    IReadOnlyList<IReadOnlyList<double?>> Values,
    IReadOnlyList<int> Samples,
    IReadOnlyList<IReadOnlyList<Distribution?>>? Distributions);

public static class ReportWire {
    private const uint Magic = 0x53524745;
    private const byte FormatVersion = 1;

    public static string WriteCube(ReportCube cube) {
        ArgumentNullException.ThrowIfNull(cube);
        return JsonSerializer.Serialize(ToWire(cube), ReportJsonContext.Default.CubeWire);
    }

    public static ReportCube ReadCube(string json) {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var wire = JsonSerializer.Deserialize(json, ReportJsonContext.Default.CubeWire) ?? throw new JsonException("cube wire is null");
        return FromWire(wire);
    }

    public static string WriteQuery(ReportQuery q) {
        ArgumentNullException.ThrowIfNull(q);
        return JsonSerializer.Serialize(q, ReportJsonContext.Default.ReportQuery);
    }

    public static ReportQuery ReadQuery(string json) {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize(json, ReportJsonContext.Default.ReportQuery) ?? throw new JsonException("report query is null");
    }

    public static string WriteDescriptor(SourceDescriptor d) {
        ArgumentNullException.ThrowIfNull(d);
        return JsonSerializer.Serialize(d, ReportJsonContext.Default.SourceDescriptor);
    }

    public static SourceDescriptor ReadDescriptor(string json) {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize(json, ReportJsonContext.Default.SourceDescriptor) ?? throw new JsonException("source descriptor is null");
    }

    public static byte[] WriteSnapshot(RowSnapshot s) {
        ArgumentNullException.ThrowIfNull(s);
        var writer = new ArrayBufferWriter<byte>();
        WriteUInt32(writer, Magic);
        WriteByte(writer, FormatVersion);
        WriteString(writer, s.Source);
        WriteString(writer, s.Version);
        WriteInt32(writer, s.Rows);
        WriteInt32(writer, s.Columns.Count);
        foreach (var column in s.Columns) WriteColumn(writer, column, s.Rows);
        return writer.WrittenSpan.ToArray();
    }

    public static RowSnapshot ReadSnapshot(ReadOnlySpan<byte> bytes) {
        var offset = 0;
        if (ReadUInt32(bytes, ref offset) != Magic) throw new InvalidDataException("bad snapshot magic");
        var version = ReadByte(bytes, ref offset);
        if (version != FormatVersion) throw new InvalidDataException($"unsupported snapshot format version {version}");
        var source = ReadString(bytes, ref offset);
        var snapshotVersion = ReadString(bytes, ref offset);
        var rows = ReadInt32(bytes, ref offset);
        var columnCount = ReadInt32(bytes, ref offset);
        var columns = new List<SnapshotColumn>(columnCount);
        for (var i = 0; i < columnCount; i++) columns.Add(ReadColumn(bytes, ref offset, rows));
        return new RowSnapshot(source, snapshotVersion, rows, columns);
    }

    private static CubeWire ToWire(ReportCube cube) {
        var rows = cube.Rows.Count;
        var dimCount = cube.Dimensions.Count;
        var measureCount = cube.Measures.Count;

        var keys = new List<IReadOnlyList<string>>(dimCount);
        for (var d = 0; d < dimCount; d++) {
            var column = new string[rows];
            for (var r = 0; r < rows; r++) column[r] = cube.Rows[r].Keys[d];
            keys.Add(column);
        }

        var values = new List<IReadOnlyList<double?>>(measureCount);
        for (var m = 0; m < measureCount; m++) {
            var column = new double?[rows];
            for (var r = 0; r < rows; r++) column[r] = cube.Rows[r].Values[m];
            values.Add(column);
        }

        var samples = new int[rows];
        for (var r = 0; r < rows; r++) samples[r] = cube.Rows[r].Samples;

        List<IReadOnlyList<Distribution?>>? distributions = null;
        if (cube.Rows.Any(r => r.Distributions.Any(d => d is not null))) {
            distributions = [];
            for (var m = 0; m < measureCount; m++) {
                var column = new Distribution?[rows];
                for (var r = 0; r < rows; r++) column[r] = m < cube.Rows[r].Distributions.Count ? cube.Rows[r].Distributions[m] : null;
                distributions.Add(column);
            }
        }

        return new CubeWire(cube.Dimensions, cube.Measures, cube.Normalized, rows, keys, values, samples, distributions);
    }

    private static ReportCube FromWire(CubeWire wire) {
        var dimCount = wire.Dimensions.Count;
        var measureCount = wire.Measures.Count;
        var rows = new List<CubeRow>(wire.Rows);
        for (var r = 0; r < wire.Rows; r++) {
            var keys = new string[dimCount];
            for (var d = 0; d < dimCount; d++) keys[d] = wire.Keys[d][r];
            var values = new double?[measureCount];
            for (var m = 0; m < measureCount; m++) values[m] = wire.Values[m][r];
            var row = new CubeRow(keys, values, wire.Samples[r]);
            if (wire.Distributions is { } distributions) {
                var rowDistributions = new Distribution?[measureCount];
                for (var m = 0; m < measureCount; m++) rowDistributions[m] = distributions[m][r];
                row = row with { Distributions = rowDistributions };
            }
            rows.Add(row);
        }
        return new ReportCube(wire.Dimensions, wire.Measures, wire.Normalized, rows);
    }

    private static void WriteColumn(ArrayBufferWriter<byte> writer, SnapshotColumn column, int rows) {
        WriteString(writer, column.Key);
        WriteByte(writer, (byte)column.Kind);
        WriteInt32(writer, column.Dictionary.Length);
        foreach (var entry in column.Dictionary) WriteString(writer, entry);
        WriteBitmap(writer, column.Present, rows);

        if (column.Numbers is { } numbers) {
            foreach (var n in numbers) WriteDouble(writer, n);
        } else if (column.Dates is { } dates) {
            foreach (var d in dates) WriteInt64(writer, d);
        } else if (column.Bools is { } bools) {
            WriteBitmap(writer, BoolsToBits(bools), rows);
        } else if (column.ManyOffsets is { } offsets && column.ManyItems is { } items) {
            foreach (var o in offsets) WriteInt32(writer, o);
            foreach (var item in items) WriteInt32(writer, item);
        } else if (column.Text is { } text) {
            foreach (var t in text) WriteInt32(writer, t);
        }
    }

    private static SnapshotColumn ReadColumn(ReadOnlySpan<byte> bytes, ref int offset, int rows) {
        var key = ReadString(bytes, ref offset);
        var kind = (FilterValueKind)ReadByte(bytes, ref offset);
        var dictionaryCount = ReadInt32(bytes, ref offset);
        var dictionary = new string[dictionaryCount];
        for (var i = 0; i < dictionaryCount; i++) dictionary[i] = ReadString(bytes, ref offset);
        var present = ReadUInt64Array(bytes, ref offset, BitmapWords(rows));

        var column = new SnapshotColumn(key, kind, rows) {
            Present = present,
            Dictionary = dictionary,
        };

        switch (kind) {
            case FilterValueKind.Number:
                column.Numbers = ReadDoubleArray(bytes, ref offset, rows);
                break;
            case FilterValueKind.Date:
                column.Dates = ReadInt64Array(bytes, ref offset, rows);
                break;
            case FilterValueKind.Bool:
                column.Bools = BitsToBools(ReadUInt64Array(bytes, ref offset, BitmapWords(rows)), rows);
                break;
            case FilterValueKind.Set:
                var offsets = ReadInt32Array(bytes, ref offset, rows + 1);
                var itemCount = rows == 0 ? 0 : offsets[rows];
                column.ManyOffsets = offsets;
                column.ManyItems = ReadInt32Array(bytes, ref offset, itemCount);
                break;
            default:
                column.Text = ReadInt32Array(bytes, ref offset, rows);
                break;
        }

        return column;
    }

    private static int BitmapWords(int rows) => (rows + 63) / 64;

    private static void WriteBitmap(ArrayBufferWriter<byte> writer, ulong[] bits, int rows) {
        var words = BitmapWords(rows);
        for (var i = 0; i < words; i++) WriteUInt64(writer, i < bits.Length ? bits[i] : 0UL);
    }

    private static bool[] BitsToBools(ulong[] bits, int rows) {
        var result = new bool[rows];
        for (var i = 0; i < rows; i++) result[i] = (bits[i >> 6] & (1UL << (i & 63))) != 0;
        return result;
    }

    private static ulong[] BoolsToBits(bool[] values) {
        var bits = new ulong[BitmapWords(values.Length)];
        for (var i = 0; i < values.Length; i++)
            if (values[i]) bits[i >> 6] |= 1UL << (i & 63);
        return bits;
    }

    private static void WriteByte(ArrayBufferWriter<byte> writer, byte value) {
        var span = writer.GetSpan(1);
        span[0] = value;
        writer.Advance(1);
    }

    private static void WriteInt32(ArrayBufferWriter<byte> writer, int value) {
        var span = writer.GetSpan(4);
        BinaryPrimitives.WriteInt32LittleEndian(span, value);
        writer.Advance(4);
    }

    private static void WriteUInt32(ArrayBufferWriter<byte> writer, uint value) {
        var span = writer.GetSpan(4);
        BinaryPrimitives.WriteUInt32LittleEndian(span, value);
        writer.Advance(4);
    }

    private static void WriteInt64(ArrayBufferWriter<byte> writer, long value) {
        var span = writer.GetSpan(8);
        BinaryPrimitives.WriteInt64LittleEndian(span, value);
        writer.Advance(8);
    }

    private static void WriteUInt64(ArrayBufferWriter<byte> writer, ulong value) {
        var span = writer.GetSpan(8);
        BinaryPrimitives.WriteUInt64LittleEndian(span, value);
        writer.Advance(8);
    }

    private static void WriteDouble(ArrayBufferWriter<byte> writer, double value) {
        var span = writer.GetSpan(8);
        BinaryPrimitives.WriteInt64LittleEndian(span, BitConverter.DoubleToInt64Bits(value));
        writer.Advance(8);
    }

    private static void WriteString(ArrayBufferWriter<byte> writer, string value) {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        var span = writer.GetSpan(4 + byteCount);
        BinaryPrimitives.WriteInt32LittleEndian(span, byteCount);
        Encoding.UTF8.GetBytes(value, span[4..]);
        writer.Advance(4 + byteCount);
    }

    private static byte ReadByte(ReadOnlySpan<byte> bytes, ref int offset) {
        var value = bytes[offset];
        offset += 1;
        return value;
    }

    private static int ReadInt32(ReadOnlySpan<byte> bytes, ref int offset) {
        var value = BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]);
        offset += 4;
        return value;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, ref int offset) {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
        offset += 4;
        return value;
    }

    private static string ReadString(ReadOnlySpan<byte> bytes, ref int offset) {
        var length = ReadInt32(bytes, ref offset);
        var value = Encoding.UTF8.GetString(bytes.Slice(offset, length));
        offset += length;
        return value;
    }

    private static double[] ReadDoubleArray(ReadOnlySpan<byte> bytes, ref int offset, int count) {
        var byteLength = count * sizeof(double);
        var result = new double[count];
        MemoryMarshal.Cast<byte, double>(bytes.Slice(offset, byteLength)).CopyTo(result);
        offset += byteLength;
        return result;
    }

    private static long[] ReadInt64Array(ReadOnlySpan<byte> bytes, ref int offset, int count) {
        var byteLength = count * sizeof(long);
        var result = new long[count];
        MemoryMarshal.Cast<byte, long>(bytes.Slice(offset, byteLength)).CopyTo(result);
        offset += byteLength;
        return result;
    }

    private static int[] ReadInt32Array(ReadOnlySpan<byte> bytes, ref int offset, int count) {
        var byteLength = count * sizeof(int);
        var result = new int[count];
        MemoryMarshal.Cast<byte, int>(bytes.Slice(offset, byteLength)).CopyTo(result);
        offset += byteLength;
        return result;
    }

    private static ulong[] ReadUInt64Array(ReadOnlySpan<byte> bytes, ref int offset, int count) {
        var byteLength = count * sizeof(ulong);
        var result = new ulong[count];
        MemoryMarshal.Cast<byte, ulong>(bytes.Slice(offset, byteLength)).CopyTo(result);
        offset += byteLength;
        return result;
    }
}
