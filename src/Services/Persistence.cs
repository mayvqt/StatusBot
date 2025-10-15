using Newtonsoft.Json;
using ServiceStatusBot.Models;

namespace ServiceStatusBot.Services;

public class Persistence
{
    private readonly string _statePath = "state.json";
    public State State { get; private set; } = new();

    public Persistence()
    {
        LoadState();
    }

    public void LoadState()
    {
        if (File.Exists(_statePath))
        {
            var json = File.ReadAllText(_statePath);
            State = JsonConvert.DeserializeObject<State>(json) ?? new State();
        }
    }

    public void SaveState()
    {
        var tempPath = _statePath + ".tmp";
        File.WriteAllText(tempPath, JsonConvert.SerializeObject(State, Formatting.Indented));
        if (File.Exists(_statePath))
        {
            File.Replace(tempPath, _statePath, null);
        }
        else
        {
            File.Move(tempPath, _statePath);
        }
    }
}
