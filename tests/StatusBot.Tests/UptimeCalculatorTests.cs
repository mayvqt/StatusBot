using System;
using ServiceStatusBot.Models;
using ServiceStatusBot.Services;
using Xunit;

namespace StatusBot.Tests;

public class UptimeCalculatorTests
{
    [Fact]
    public void SteadyOnline_AccumulatesUpTime()
    {
        var start = DateTime.UtcNow;
        var prev = new ServiceStatus
        {
            MonitoringSince = start,
            LastChecked = start,
            Online = true,
            CumulativeUpSeconds = 0
        };

        var current = new ServiceStatus();
        var now = start.AddSeconds(30);
        UptimeCalculator.UpdateUptime(prev, current, true, now);

        Assert.Equal(now, current.LastChecked);
        Assert.True(current.CumulativeUpSeconds >= 30 - 0.001);
        Assert.InRange(current.UptimePercent, 99.0, 101.0);
    }

    [Fact]
    public void FlipOffline_StopsAccumulation()
    {
        var start = DateTime.UtcNow;
        var prev = new ServiceStatus
        {
            MonitoringSince = start,
            LastChecked = start,
            Online = true,
            CumulativeUpSeconds = 0
        };

        var current = new ServiceStatus();
        var now = start.AddSeconds(30);
        UptimeCalculator.UpdateUptime(prev, current, false, now);

        Assert.Equal(now, current.LastChecked);
        // cumulative should contain the 30s the prev was online
        Assert.True(current.CumulativeUpSeconds >= 30 - 0.001);
        // Now overall uptime percent should be less than 100
        Assert.InRange(current.UptimePercent, 0.0, 100.0);
        Assert.Equal(false, current.Online);
    }

    [Fact]
    public void MonitoringSince_PersistedAcrossRestart()
    {
        var start = DateTime.UtcNow.AddHours(-1);
        var prev = new ServiceStatus
        {
            MonitoringSince = start,
            LastChecked = start.AddMinutes(30),
            Online = true,
            CumulativeUpSeconds = 1800 // 30 minutes
        };

        var current = new ServiceStatus();
        var now = start.AddMinutes(31);
        UptimeCalculator.UpdateUptime(prev, current, true, now);

        Assert.Equal(prev.MonitoringSince, current.MonitoringSince);
        Assert.True(current.CumulativeUpSeconds >= 1860 - 0.1); // previous 1800 + elapsed 60s
    }
}
