using CommunityToolkit.Mvvm.ComponentModel;
using DataSpecter.UI.Models;
using System;

namespace DataSpecter.UI.ViewModels
{
    public partial class AnomalyDetailsViewModel : ObservableObject
    {
        [ObservableProperty]
        private FileModel? _file;
        
        [ObservableProperty]
        private long _offset;
        
        [ObservableProperty]
        private long _length;
        
        [ObservableProperty]
        private byte[]? _data;
        
        [ObservableProperty]
        private byte[]? _hexData; // Using byte[] for ItemsControl binding in UI

        [ObservableProperty]
        private string _textData = string.Empty;

        public void Initialize(FileModel file, long offset, long length)
        {
            File = file;
            Offset = offset;
            Length = length;

            if (File?.DataSource != null)
            {
                // Cap length for display safety (e.g. 1MB max for detail view)
                int safeLength = (int)Math.Min(length, 1024 * 1024);
                byte[] buffer = new byte[safeLength];
                
                int read = File.DataSource.ReadRange(offset, buffer, 0, safeLength);
                
                if (read < safeLength)
                {
                    Array.Resize(ref buffer, read);
                }

                Data = buffer;
                HexData = buffer;
                TextData = System.Text.Encoding.UTF8.GetString(buffer);
            }
        }
    }
}
