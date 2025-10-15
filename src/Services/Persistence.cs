using Newtonsoft.Json;
using ServiceStatusBot.Models;

namespace ServiceStatusBot.Services;

public class Persistence
{
    private readonly string _statePath = "state.json";
    public State State { get; private set; } = new();

    public Persistence()
    {
        try
        {
            LoadState();
        }
        catch (Exception ex)
        {
            ErrorHelper.LogError("Failed to load state during Persistence initialization", ex);
            State = new State();
        }
    }

    public void LoadState()
    {
        try
        {
            if (File.Exists(_statePath))
            {
                var json = File.ReadAllText(_statePath);
                State = JsonConvert.DeserializeObject<State>(json) ?? new State();
            }
        }
        catch (Exception ex)
        {
            ErrorHelper.LogError($"Error loading state from '{_statePath}'", ex);
            State = new State();
        }
    }

    public void SaveState()
    {
        try
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
        catch (Exception ex)
        {
            ErrorHelper.LogError($"Error saving state to '{_statePath}'", ex);
            // Best-effort: leave in-memory state intact
        }
    }
}
