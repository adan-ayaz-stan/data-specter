using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace DataSpecter.Core.Models
{
    /// <summary>
    /// Represents a handle to a forensic target file.
    /// abstracting away whether it is in RAM or on Disk.
    /// </summary>
    public class BinaryDataSource : IDisposable
    {
        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        private bool _disposed;

        public string FilePath { get; }
        public long Length { get; }

        public BinaryDataSource(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Target file not found", filePath);

            FilePath = filePath;
            var fileInfo = new FileInfo(filePath);
            Length = fileInfo.Length;

            // Map the file into virtual memory. 
            // "Capacity: 0" means map the whole file.
            _mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);

            // Create a view accessor to read bytes randomly.
            _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        }

        /// <summary>
        /// Reads a single byte at the specified index.
        /// Thread-safe and fast.
        /// </summary>
        public byte ReadByte(long offset)
        {
            if (offset < 0 || offset >= Length)
                throw new IndexOutOfRangeException();

            return _accessor.ReadByte(offset);
        }

        /// <summary>
        /// Reads a chunk of bytes into a buffer.
        /// Optimized for the Hex Viewer to grab a "screen's worth" of rows.
        /// </summary>
        public int ReadRange(long offset, byte[] buffer, int index, int count)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BinaryDataSource));

            // Clamp count if we are near the end of file
            long remaining = Length - offset;
            if (remaining <= 0) return 0;

            int toRead = (int)Math.Min(count, remaining);
            return _accessor.ReadArray(offset, buffer, index, toRead);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _accessor?.Dispose();
                _mmf?.Dispose();
                _disposed = true;
            }
        }
    }
}