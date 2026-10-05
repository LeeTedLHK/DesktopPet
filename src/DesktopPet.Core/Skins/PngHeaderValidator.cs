using System.Buffers.Binary;
using System.Text;

namespace DesktopPet.Skins;

/// <summary>Checks the PNG container and IHDR. The WPF switch preflight must fully decode pixels.</summary>
internal static class PngHeaderValidator
{
    public static long Validate(string path)
    {
        using var stream = File.OpenRead(path);
        try
        {
            Span<byte> header = stackalloc byte[33];
            stream.ReadExactly(header);
            if (!header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
                BinaryPrimitives.ReadUInt32BigEndian(header[8..12]) != 13 ||
                !header[12..16].SequenceEqual("IHDR"u8))
                throw Invalid(path, "invalid PNG signature or IHDR");

            var width = BinaryPrimitives.ReadUInt32BigEndian(header[16..20]);
            var height = BinaryPrimitives.ReadUInt32BigEndian(header[20..24]);
            if (width is < 32 or > 2048 || height is < 32 or > 2048)
                throw Invalid(path, "PNG dimensions must be between 32 and 2048 pixels");
            var validDepth = header[25] switch
            {
                0 => header[24] is 1 or 2 or 4 or 8 or 16,
                2 or 4 or 6 => header[24] is 8 or 16,
                3 => header[24] is 1 or 2 or 4 or 8,
                _ => false,
            };
            if (!validDepth || header[26] != 0 || header[27] != 0 || header[28] > 1)
                throw Invalid(path, "invalid PNG IHDR fields");
            var crc = 0xFFFFFFFFu;
            foreach (var value in header[12..29])
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++)
                    crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xEDB88320u : 0);
            }
            if (~crc != BinaryPrimitives.ReadUInt32BigEndian(header[29..33]))
                throw Invalid(path, "invalid PNG IHDR checksum");

            var hasImageData = false;
            var hasPalette = false;
            Span<byte> chunkHeader = stackalloc byte[8];
            while (stream.Position < stream.Length)
            {
                stream.ReadExactly(chunkHeader);
                var length = BinaryPrimitives.ReadUInt32BigEndian(chunkHeader[..4]);
                if (length > stream.Length - stream.Position - 4)
                    throw Invalid(path, "truncated PNG chunk");
                foreach (var character in chunkHeader[4..8])
                    if (character is not (>= (byte)'A' and <= (byte)'Z') and not (>= (byte)'a' and <= (byte)'z'))
                        throw Invalid(path, "invalid PNG chunk type");
                var kind = Encoding.ASCII.GetString(chunkHeader[4..8]);
                if (kind == "IHDR") throw Invalid(path, "duplicate PNG IHDR");
                if (kind == "PLTE")
                {
                    if (hasImageData || hasPalette || length == 0 || length > 768 || length % 3 != 0)
                        throw Invalid(path, "invalid PNG palette");
                    hasPalette = true;
                }
                if (kind == "IDAT")
                {
                    if (header[25] == 3 && !hasPalette) throw Invalid(path, "indexed PNG has no palette");
                    hasImageData |= length > 0;
                }
                if (kind == "IEND")
                {
                    if (length != 0 || !hasImageData || stream.Position + 4 != stream.Length)
                        throw Invalid(path, "invalid PNG end or missing image data");
                    return (long)width * height;
                }
                if (kind is not ("PLTE" or "IDAT") && chunkHeader[4] < (byte)'a')
                    throw Invalid(path, "unknown critical PNG chunk");
                stream.Position += length + 4L;
            }
            throw Invalid(path, "PNG has no IEND");
        }
        catch (EndOfStreamException) { throw Invalid(path, "truncated PNG header or container"); }
    }

    private static InvalidDataException Invalid(string path, string reason) =>
        new($"{Path.GetFileName(path)}: {reason}.");
}
