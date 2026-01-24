using Serilog.Core;
using Serilog.Events;
using System;
using CloverBridge.UI;

namespace CloverBridge.Services;

public class LogWindowSink : ILogEventSink
{
    private readonly IFormatProvider? _formatProvider;

    public LogWindowSink(IFormatProvider? formatProvider = null)
    {
        _formatProvider = formatProvider;
    }

    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage(_formatProvider);
        if (logEvent.Exception != null)
        {
            message += Environment.NewLine + logEvent.Exception.ToString();
        }
        
        // Forward to LogWindow if it exists
        LogWindow.AddLog(message);
    }
}
