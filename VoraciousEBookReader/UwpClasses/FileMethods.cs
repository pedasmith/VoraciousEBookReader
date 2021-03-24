using EpubSharp;
using PCLStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace SimpleEpubReader.UwpClasses
{
    public static class FileMethods
    {
        public static async Task<byte[]> ReadBytesAsync(string fullpath)
        {
            byte[] retval = null;
            try
            {
                var file = await PCLStorage.FileSystem.Current.GetFileFromPathAsync(fullpath);
                if (file != null)
                {
                    using (var stream = await file.OpenAsync(PCLStorage.FileAccess.Read))
                    {
                        if (stream == null)
                        {
                            App.Error($"ERROR: file exists but can't read any part of file {fullpath}");
                        }
                        else
                        {
                            retval = stream.ReadToEnd();
                        }
                    }
                }
            }
            catch (PCLStorage.Exceptions.FileNotFoundException)
            {
                App.Error($"ERROR: file doesn't exist while reading for {fullpath}");
                retval = null;
            }

            return retval;
            // Old code: var fbuffer= await PathIO.ReadBufferAsync(data.FilePath);

        }

        public static async Task WriteBytesAsync(this IFile file, List<byte> data)
        {
            using (var stream = await file.OpenAsync(FileAccess.ReadAndWrite))
            {
                var bytes = data.ToArray();
                stream.Write(bytes, 0, bytes.Count());
            }
        }
        public static async Task WriteBytesAsync(this IFile file, byte[] data)
        {
            using (var stream = await file.OpenAsync(FileAccess.ReadAndWrite))
            {
                stream.Write(data, 0, data.Count());
            }
        }
    }
}
