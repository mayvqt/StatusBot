using Newtonsoft.Json;
using ServiceStatusBot.Models;

namespace ServiceStatusBot.Services;

/// <summary>
/// Handles loading and saving the application <see cref="State"/> to disk.
/// The implementation favors safety: atomic writes with a temp file and retries
/// in case the destination file is locked by another process.
/// </summary>
public class Persistence
{
    // File path under the config directory so setup tools and configuration live together.
    private readonly string _statePath = Path.Combine("config", "state.json");

    /// <summary>In-memory application state. Consumers may read and mutate this object; callers must save via <see cref="SaveState"/>.</summary>
    public State State { get; private set; } = new();

    private readonly object _saveLock = new();

    /// <summary>Constructs a Persistence instance and attempts to load existing state.</summary>
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

    /// <summary>Loads state from disk if present. If the file is missing or invalid, starts with a fresh default <see cref="State"/>.</summary>
    public void LoadState()
    {
        try
        {
            if (File.Exists(_statePath))
            {
                var json = File.ReadAllText(_statePath);
                // Preserve the DateTime Kind so previously-saved Local/UTC values round-trip correctly.
                var settings = new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind };
                State = JsonConvert.DeserializeObject<State>(json, settings) ?? new State();

                // Defensive initialization of the dictionaries so consumers don't need to null-check.
                State.MessageMetadata ??= new Dictionary<string, MessageReference>();
                State.Statuses ??= new Dictionary<string, ServiceStatus>();
            }
        }
        catch (Exception ex)
        {
            ErrorHelper.LogError($"Error loading state from '{_statePath}'", ex);
            State = new State();
        }
    }

    /// <summary>
    /// Saves the in-memory state to disk. Uses a temp file then an atomic replace when possible.
    /// On Windows the atomic Replace may fail when an external process holds the file; the method
    /// falls back to delete+move with a few retries.
    /// </summary>
    public void SaveState()
    {
        lock (_saveLock)
        {
            try
            {
                var tempPath = _statePath + ".tmp";
                var settings = new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind };
                File.WriteAllText(tempPath, JsonConvert.SerializeObject(State, Formatting.Indented, settings));

                // Prefer an atomic replace where supported.
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
                    }

                    // Fallback path: try delete+move with a few retries in case of transient locks.
                    const int maxAttempts = 3;
                    var attempt = 0;
                    var moved = false;
                    while (attempt < maxAttempts && !moved)
                    {
                        attempt++;
                        try
                        {
                            if (File.Exists(_statePath)) File.Delete(_statePath);
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
                    // Destination doesn't exist; a simple move is fine.
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
