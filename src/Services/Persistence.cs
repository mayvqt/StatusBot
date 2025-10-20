using Newtonsoft.Json;
using ServiceStatusBot.Models;

namespace ServiceStatusBot.Services;

public class Persistence
{
    private readonly string _statePath = "state.json";
    public State State { get; private set; } = new();
    private readonly object _saveLock = new();

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
                var settings = new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.Utc };
                State = JsonConvert.DeserializeObject<State>(json, settings) ?? new State();
                // Defensive: ensure collections are non-null after deserialization
                State.MessageMetadata ??= new Dictionary<string, MessageReference>();
                State.Statuses ??= new Dictionary<string, ServiceStatus>();
                // No migration required when running fresh; keep collections initialized.
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
        lock (_saveLock)
        {
            try
            {
                var tempPath = _statePath + ".tmp";
                var settings = new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.Utc };
                File.WriteAllText(tempPath, JsonConvert.SerializeObject(State, Formatting.Indented, settings));

                // If destination exists try an atomic replace first. If that fails (locked file etc.)
                // fall back to delete+move with a few retries.
                if (File.Exists(_statePath))
                {
                    try
                    {
                        File.Replace(tempPath, _statePath, null);
                        return;
                    }
                    catch (IOException ioEx)
                    {
                        ErrorHelper.LogWarning($"File.Replace failed: {ioEx.Message}. Will attempt fallback delete+move.");
                        // fallback path below
                    }

                    // Fallback: try deleting the destination and moving the temp into place with retries
                    const int maxAttempts = 3;
                    var attempt = 0;
                    var moved = false;
                    while (attempt < maxAttempts && !moved)
                    {
                        attempt++;
                        try
                        {
                            if (File.Exists(_statePath))
                            {
                                File.Delete(_statePath);
                            }
                            File.Move(tempPath, _statePath);
                            moved = true;
                        }
                        catch (IOException ioe)
                        {
                            ErrorHelper.LogWarning($"Attempt {attempt} to replace state file failed: {ioe.Message}");
                            System.Threading.Thread.Sleep(100 * attempt);
                        }
                        catch (UnauthorizedAccessException uaex)
                        {
                            ErrorHelper.LogError("Unauthorized access while attempting to replace state file", uaex);
                            break;
                        }
                    }

                    if (!moved)
                    {
                        ErrorHelper.LogError($"Failed to write state file after {maxAttempts} attempts; temp file left at '{tempPath}'");
                    }
                }
                else
                {
                    // Destination doesn't exist; simple move
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
}
