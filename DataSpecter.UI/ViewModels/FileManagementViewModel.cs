using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataSpecter.UI.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace DataSpecter.UI.ViewModels
{
    public partial class FileManagementViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<FileModel> _files;

        public FileManagementViewModel(ObservableCollection<FileModel> files)
        {
             _files = files;
             _files.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TotalSizeMB));
        }

        [RelayCommand]
        private void DeleteFile(FileModel? file)
        {
            if (file != null)
            {
                file.DataSource?.Dispose();
                Files.Remove(file);
            }
        }
        
        public double TotalSizeMB => Files.Sum(f => f.Size) / 1024.0 / 1024.0;
    }
}
