using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Proyecto_HobbyHub.Security;

namespace Proyecto_HobbyHub.Tests;
public class ImageValidationTests
{
    public static byte[] Png(uint width=1,uint height=1)
    {
        using var stream=new MemoryStream();stream.Write(new byte[]{137,80,78,71,13,10,26,10});var header=new byte[13];BinaryPrimitives.WriteUInt32BigEndian(header,width);BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4),height);header[8]=8;header[9]=6;Chunk(stream,"IHDR",header);
        using var compressed=new MemoryStream();using(var zlib=new ZLibStream(compressed,CompressionLevel.SmallestSize,true)){zlib.Write(new byte[]{0,150,80,200,255});}Chunk(stream,"IDAT",compressed.ToArray());Chunk(stream,"IEND",Array.Empty<byte>());return stream.ToArray();
    }
    private static void Chunk(Stream stream,string type,byte[] data)
    {
        var size=new byte[4];BinaryPrimitives.WriteUInt32BigEndian(size,(uint)data.Length);stream.Write(size);var body=Encoding.ASCII.GetBytes(type).Concat(data).ToArray();stream.Write(body);uint crc=0xffffffff;foreach(byte v in body){crc^=v;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);}BinaryPrimitives.WriteUInt32BigEndian(size,crc^0xffffffff);stream.Write(size);
    }
    [Fact] public void ValidPngIsAcceptedAndMimeIsDetectedFromBytes()=>Assert.Equal("image/png",ImageUploadService.Validate(Png()).Mime);
    [Fact] public void CorruptCrcIsRejected(){var bytes=Png();bytes[25]^=1;Assert.Throws<InvalidDataException>(()=>ImageUploadService.Validate(bytes));}
    [Fact] public void OversizedDimensionsAreRejected()=>Assert.Throws<InvalidDataException>(()=>ImageUploadService.Validate(Png(10000,10000)));
    [Fact] public void ScriptRenamedToPngIsRejected()=>Assert.Throws<InvalidDataException>(()=>ImageUploadService.Validate(Encoding.UTF8.GetBytes("<svg><script>alert('synthetic')</script></svg>")));
    [Fact] public void AppendedPayloadIsRejected()=>Assert.Throws<InvalidDataException>(()=>ImageUploadService.Validate(Png().Concat(new byte[]{1,2,3}).ToArray()));
    [Fact] public void InvalidRasterLengthIsRejected()=>Assert.Throws<InvalidDataException>(()=>ImageUploadService.Validate(Png(2,2)));
    [Fact] public void UnsignedChunkOverflowIsRejectedAsInvalidImage(){var bytes=Png();BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8,4),uint.MaxValue);Assert.Throws<InvalidDataException>(()=>ImageUploadService.Validate(bytes));}
    [Fact] public void RealJpegIsDecodedAndRewritten()
    {
        using var bitmap=new SkiaSharp.SKBitmap(2,2);bitmap.Erase(SkiaSharp.SKColors.Purple);using var image=SkiaSharp.SKImage.FromBitmap(bitmap);using var encoded=image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg,90);
        var result=ImageUploadService.Validate(encoded.ToArray());Assert.Equal("image/jpeg",result.Mime);
        using var decoded=SkiaSharp.SKBitmap.Decode(result.Bytes);Assert.Equal(2,decoded.Width);Assert.Equal(2,decoded.Height);
    }
}
