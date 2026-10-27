using System.Buffers.Binary;
using SkiaSharp;

namespace Proyecto_HobbyHub.Security;

public sealed record StoredImage(string File, string Mime);
public sealed class ImageUploadService(IWebHostEnvironment environment)
{
    public const int MaxBytes = 5 * 1024 * 1024;
    private string DirectoryPath => Path.Combine(environment.ContentRootPath, ".local", "uploads");
    public async Task<StoredImage> Store(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length < 24 || file.Length > MaxBytes) throw new InvalidDataException("Cada imagen debe ser JPG o PNG y ocupar como máximo 5 MB.");
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        await DecodeGate.WaitAsync(cancellationToken);
        byte[] data;string mime;
        try{(data,mime)=Validate(stream.ToArray());}finally{DecodeGate.Release();}
        string name = Guid.NewGuid().ToString("N") + (mime == "image/png" ? ".png" : ".jpg");
        Directory.CreateDirectory(DirectoryPath);
        await System.IO.File.WriteAllBytesAsync(Path.Combine(DirectoryPath, name), data, cancellationToken);
        return new(name, mime);
    }
    public string PathFor(string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-f0-9]{32}\\.(png|jpg)$")) throw new InvalidDataException("Nombre de archivo inválido.");
        return Path.Combine(DirectoryPath, name);
    }
    public void Remove(string name) => System.IO.File.Delete(PathFor(name));
    public static (byte[] Bytes, string Mime) Validate(byte[] bytes)
    {
        if (bytes.Length > MaxBytes || bytes.Length < 24) throw new InvalidDataException("Imagen demasiado grande o incompleta.");
        if (bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}))
        {
            using var output = new MemoryStream(); output.Write(bytes, 0, 8);
            bool header = false, raster = false, end = false; int offset = 8;
            long expected=0;using var compressed=new MemoryStream();
            while (offset + 12 <= bytes.Length)
            {
                uint chunkLength=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset,4));
                if(chunkLength>int.MaxValue)throw new InvalidDataException("PNG incompleto.");
                int size = (int)chunkLength;
                if (size < 0 || size > bytes.Length - offset - 12) throw new InvalidDataException("PNG incompleto.");
                string type = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, 4);
                uint crc = 0xffffffff;
                for (int i = offset + 4; i < offset + 8 + size; i++) {
                    crc ^= bytes[i]; for (int b=0;b<8;b++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
                }
                if ((crc ^ 0xffffffff) != BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8 + size,4))) throw new InvalidDataException("PNG dañado.");
                if (!header && type != "IHDR") throw new InvalidDataException("PNG sin cabecera.");
                if (type == "IHDR") {
                    if (header || size != 13) throw new InvalidDataException("Cabecera inválida.");
                    Dimensions(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset+8,4)), BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset+12,4)));
                    uint width=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset+8,4)),height=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset+12,4));
                    int depth=bytes[offset+16],color=bytes[offset+17];
                    int channels=color switch{0=>1,2=>3,3=>1,4=>2,6=>4,_=>0};
                    if(channels==0 || depth is not(1 or 2 or 4 or 8 or 16) ||
                       ((color is 2 or 4 or 6)&&depth<8) || (color==3&&depth==16) ||
                       bytes[offset+18]!=0||bytes[offset+19]!=0||bytes[offset+20]!=0)
                        throw new InvalidDataException("PNG no compatible. Usa una imagen sin entrelazado.");
                    expected=(((long)width*channels*depth+7)/8+1)*height;
                    header = true;
                }
                if (type == "IDAT") {raster = true;compressed.Write(bytes,offset+8,size);}
                if (type is "IHDR" or "PLTE" or "tRNS" or "IDAT" or "IEND") output.Write(bytes,offset,size+12);
                offset += size + 12;
                if (type == "IEND") { end = size == 0 && offset == bytes.Length; break; }
            }
            if (!header || !raster || !end) throw new InvalidDataException("PNG incompleto.");
            compressed.Position=0;
            using(var zlib=new System.IO.Compression.ZLibStream(compressed,System.IO.Compression.CompressionMode.Decompress)) {
                long decoded=0;var buffer=new byte[8192];int read;
                while((read=zlib.Read(buffer,0,buffer.Length))>0){decoded+=read;if(decoded>expected)throw new InvalidDataException("Datos PNG inválidos.");}
                if(decoded!=expected)throw new InvalidDataException("Datos PNG incompletos.");
            }
            return Normalize(output.ToArray(), "image/png");
        }
        if (bytes[0] == 255 && bytes[1] == 216 && bytes[^2] == 255 && bytes[^1] == 217)
        {
            using var output = new MemoryStream(); output.Write(bytes,0,2);
            int offset=2; bool dimensions=false, scan=false;
            while (offset + 4 <= bytes.Length)
            {
                int start=offset;
                if (bytes[offset++] != 255) throw new InvalidDataException("JPEG inválido.");
                while (offset < bytes.Length && bytes[offset] == 255) offset++;
                if(offset+3>bytes.Length)throw new InvalidDataException("JPEG incompleto.");
                byte marker=bytes[offset++];
                int size=BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset,2));
                if (size < 2 || offset+size > bytes.Length) throw new InvalidDataException("JPEG incompleto.");
                if (marker is 0xc0 or 0xc1 or 0xc2) {
                    if (size < 8) throw new InvalidDataException("JPEG inválido.");
                    Dimensions(BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset+5,2)), BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset+3,2)));
                    dimensions=true;
                }
                if (marker == 0xda) { output.Write(bytes,start,bytes.Length-start); scan=true; break; }
                if (!(marker >= 0xe1 && marker <= 0xef) && marker != 0xfe) output.Write(bytes,start,offset+size-start);
                offset += size;
            }
            if (!dimensions || !scan) throw new InvalidDataException("JPEG sin datos de imagen.");
            return Normalize(output.ToArray(), "image/jpeg");
        }
        throw new InvalidDataException("Solo se admiten imágenes JPG y PNG; SVG y archivos ejecutables no están permitidos.");
    }
    private static void Dimensions(uint width, uint height)
    {
        if (width == 0 || height == 0 || width > 6000 || height > 6000 || (long)width * height > 20_000_000)
            throw new InvalidDataException("La imagen supera las dimensiones permitidas.");
    }
    private static readonly SemaphoreSlim DecodeGate=new(2);
    private static (byte[],string) Normalize(byte[] bytes,string mime)
    {
        using var stream=new SKMemoryStream(bytes);using var codec=SKCodec.Create(stream);
        if(codec is null)throw new InvalidDataException("La imagen no puede decodificarse.");
        Dimensions((uint)codec.Info.Width,(uint)codec.Info.Height);
        using var bitmap=new SKBitmap(new SKImageInfo(codec.Info.Width,codec.Info.Height,SKColorType.Rgba8888,SKAlphaType.Premul));
        if(codec.GetPixels(bitmap.Info,bitmap.GetPixels())!=SKCodecResult.Success)throw new InvalidDataException("La imagen contiene datos inválidos o incompletos.");
        using var image=SKImage.FromBitmap(bitmap);using var encoded=image.Encode(mime=="image/png"?SKEncodedImageFormat.Png:SKEncodedImageFormat.Jpeg,85);
        if(encoded is null||encoded.Size>MaxBytes)throw new InvalidDataException("La imagen normalizada supera 5 MB.");
        return(encoded.ToArray(),mime);
    }
}
