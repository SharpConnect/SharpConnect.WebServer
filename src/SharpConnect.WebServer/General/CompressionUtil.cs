//MIT, 2016-2017 
using System;
using System.IO;
using System.IO.Compression;
namespace SharpConnect.WebServers
{

    public static class CompressionUtils
    {
        public static byte[] DeflateCompress(byte[] orgBuffer)
        {
            using (MemoryStream ms = new MemoryStream())
            using (DeflateStream compressedzipStream = new DeflateStream(ms, CompressionMode.Compress, true))
            {

                compressedzipStream.Write(orgBuffer, 0, orgBuffer.Length);
                compressedzipStream.Close();

                ms.Position = 0;
                byte[] compressedData = ms.ToArray();
                ms.Close();
                return compressedData;
            }
        }
        public static byte[] DeflateDecompress(byte[] compressedBuffer)
        {

            using (MemoryStream decompressedMs = new MemoryStream())
            using (MemoryStream ms2 = new MemoryStream())
            using (DeflateStream compressStream = new DeflateStream(ms2, CompressionMode.Decompress))
            {
                ms2.Write(compressedBuffer, 0, compressedBuffer.Length);
                ms2.Position = 0;
                int totalCount = ReadAllBytesFromStream(compressStream, decompressedMs);
                byte[] decompressedBuffer = decompressedMs.ToArray();

                return decompressedBuffer;
            }
        }
        //
        public static byte[] GZipCompress(byte[] orgBuffer)
        {
            using (MemoryStream ms = new MemoryStream())
            using (GZipStream compressedzipStream = new GZipStream(ms, CompressionMode.Compress, true))
            {

                compressedzipStream.Write(orgBuffer, 0, orgBuffer.Length);
                compressedzipStream.Close();
                // Reset the memory stream position to begin decompression.
                ms.Position = 0;
                byte[] compressedData = ms.ToArray();
                ms.Close();
                return compressedData;
            }
        }

        public static byte[] GZipDecompress(byte[] compressedBuffer)
        {
            using (MemoryStream decompressedMs = new MemoryStream())
            using (MemoryStream ms2 = new MemoryStream())
            using (GZipStream zipStream = new GZipStream(ms2, CompressionMode.Decompress))
            {
                ms2.Write(compressedBuffer, 0, compressedBuffer.Length);
                ms2.Position = 0;
                int totalCount = ReadAllBytesFromStream(zipStream, decompressedMs);
                byte[] decompressedBuffer = decompressedMs.ToArray();

                return decompressedBuffer;
            }
        }


        [ThreadStatic]
        static byte[] s_temp_buffer;
        static byte[] GetTempBuffer()
        {
            return s_temp_buffer ?? (s_temp_buffer = new byte[1024]);
        }
        static int ReadAllBytesFromStream(Stream compressStream, MemoryStream outputStream)
        {
            // Use this method is used to read all bytes from a stream.
            int offset = 0;
            int totalCount = 0;
            byte[] buffer = GetTempBuffer();
            while (true)
            {
                //read into buffer
                int bytesRead = compressStream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    break;
                }
                outputStream.Write(buffer, 0, bytesRead);
                offset += bytesRead;
                totalCount += bytesRead;
            }
            return totalCount;
        }

    }
}