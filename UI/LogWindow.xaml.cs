using System;
using System.Collections.Generic;
using System.Windows;

namespace CloverBridge.UI;

public partial class LogWindow : Window
{
    private static LogWindow? _instance;
    private static readonly List<string> _logBuffer = new();
    private static readonly int _maxBufferSize = 1000;

    public LogWindow()
    {
        InitializeComponent();
        _instance = this;
        
        // Cargar logs previos del buffer
        lock (_logBuffer)
        {
            foreach (var log in _logBuffer)
            {
                LogTextBox.AppendText(log + Environment.NewLine);
            }
        }
        LogTextBox.ScrollToEnd();
        
        this.Closed += (s, e) => _instance = null;
    }

    public static void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var entry = $"[{timestamp}] {message}";
        
        lock (_logBuffer)
        {
            _logBuffer.Add(entry);
            if (_logBuffer.Count > _maxBufferSize)
                _logBuffer.RemoveAt(0);
        }

        _instance?.Dispatcher.Invoke(() =>
        {
            _instance.LogTextBox.AppendText(entry + Environment.NewLine);
            _instance.LogTextBox.ScrollToEnd();
        });
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        LogTextBox.Clear();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}
