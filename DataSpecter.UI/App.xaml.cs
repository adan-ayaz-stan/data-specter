using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using DataSpecter.Core.Interfaces;
using DataSpecter.Infrastructure.Services;
using DataSpecter.UI.ViewModels; // Assuming you'll make this next

namespace DataSpecter.UI
{
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Show the main window with its ViewModel injected
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Register Services
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<ISuffixArrayService, GpuSuffixArrayService>();
            services.AddSingleton<IEntropyService, EntropyService>();
            services.AddSingleton<ILcsService, LcsService>();
            services.AddSingleton<IFuzzyHashService, FuzzyHashService>();

            // Register ViewModels
            services.AddTransient<MainViewModel>();

            // Register Views
            services.AddTransient<MainWindow>();
        }
    }
}