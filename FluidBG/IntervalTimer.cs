using System;
using System.Windows.Threading;

namespace FluidBG; 

public class IntervalTimer {
    private DispatcherTimer? Timer { get; }
    private DateTime? LastTick { get; set; }

    public IntervalTimer(double interval, Action tickFunction) {
        DispatcherTimer intervalTimer = new DispatcherTimer();
        intervalTimer.Interval = TimeSpan.FromSeconds(interval);
        intervalTimer.Tick += (sender, e) => {
            LastTick = DateTime.Now;
            tickFunction();
        };
        Timer = intervalTimer;
    }
    
    public void Start() {
        Timer?.Start();
        LastTick = DateTime.Now;
    }
    
    public void ChangeInterval(double interval) {
        Timer.Interval = TimeSpan.FromSeconds(interval);
    }
    
    public void ChangeTickFunction(Action tickFunction) {
        Timer.Tick += (sender, e) => {
            LastTick = DateTime.Now;
            tickFunction();
        };
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