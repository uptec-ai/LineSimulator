using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Threading;
using TestMcAlgorithm.Converters;

namespace TestMcAlgorithm;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application, INotifyPropertyChanged
{
    public ConvertFunction ConvertFunction { get; } = new ConvertFunction();
    private DispatcherTimer dt_timer; // 현재시간 타이머 
    public event PropertyChangedEventHandler? PropertyChanged;
    private string _currentTime = string.Empty;
    public string CurrentTime
    {
        get => _currentTime;
        set
        {
            if (_currentTime == value)
            {
                return;
            }

            _currentTime = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentTime)));
        }
    }
    public App()
    {
        AssemblyLoadContext.Default.Resolving += ResolveLocalAssembly;

        dt_timer = new DispatcherTimer();
        dt_timer.Interval = new TimeSpan(0, 0, 1);  //1초간격 동작
        dt_timer.Tick += (s, e) =>

        {
            CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        };
        dt_timer.Start();

        //PropertyChanged += StatusManager_PropertyChanged;
    }
    private void StatusManager_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        
    }
    private static Assembly? ResolveLocalAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        var candidatePath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName.Name}.dll");
        if (!File.Exists(candidatePath))
        {
            return null;
        }

        return context.LoadFromAssemblyPath(candidatePath);
    }
}
