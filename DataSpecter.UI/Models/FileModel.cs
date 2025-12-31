using CommunityToolkit.Mvvm.ComponentModel;
using DataSpecter.Core.Models;
using System;

namespace DataSpecter.UI.Models
{
    public partial class FileModel : ObservableObject
    {
        [ObservableProperty]
        private int _id;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _path = string.Empty;

        [ObservableProperty]
        private long _size;

        [ObservableProperty]
        private string _status = "Pending";
        
        [ObservableProperty]
        private long _suffixArrayCount;

        [ObservableProperty]
        private TimeSpan _suffixArrayTime;
        
        [ObservableProperty]
        private long _lcpArrayCount;

        [ObservableProperty]
        private TimeSpan _lcpArrayTime;

        [ObservableProperty]
        private string _indexingStage = string.Empty;

        [ObservableProperty]
        private int _indexingProgress;

        [ObservableProperty]
        private string _indexingDetails = string.Empty;

        public int[]? SuffixArray { get; set; }
        public int[]? LcpArray { get; set; }

        public BinaryDataSource? DataSource { get; set; }
    }
}
