using System.Collections.Concurrent;
using System.Collections.Generic;
using In.ProjectEKA.HipService.OpenMrs.HealthCheck;

public class HealthCheckStatus : IHealthCheckStatus
{
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> statusData =
        new ConcurrentDictionary<string, Dictionary<string, string>>();

    public void AddStatus(string key, Dictionary<string, string> value)
    {
        statusData[key] = value;
    }

    public Dictionary<string, string> GetStatus(string key)
    {
        statusData.TryGetValue(key, out var value);
        return value;
    }
}
