using System;
using System.Windows.Threading;

namespace FluidBG; 

public class IntervalTimer {
    public DispatcherTimer? Timer { get; }
    public DateTime? LastTick { get; set; }

    public IntervalTimer(double interval, Action tickFunction) {
        Timer = new DispatcherTimer();
        Timer.Interval = TimeSpan.FromSeconds(interval);
        Timer.Tick += (sender, e) => {
            LastTick = DateTime.Now;
            tickFunction();
        };
    }
    
    public void Start() {
        Timer?.Start();
        LastTick = DateTime.Now;
    }
    public void ResetTimer() {
        Timer.Stop();
        Timer.Start();
        LastTick = DateTime.Now;
    }
    public void ChangeInterval(double interval) {
        Timer.Interval = TimeSpan.FromSeconds(interval);
        ResetTimer();
    }
    
    
    public void Stop() {
        Timer?.Stop();
    }

    public string QueryNextTickTimestamp() {
        if (LastTick == null) return "Never";
        DateTime NextTick = LastTick.Value.AddSeconds(Timer.Interval.TotalSeconds);
        if (NextTick.Date == DateTime.Now.Date) {
            return NextTick.ToString("HH:mm:ss");
        }
        return NextTick.ToString("dd/MM/yy HH:mm:ss");

    }
}