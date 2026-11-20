using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OpenClaw.Shared.Inference;

/// <summary>
/// Persists and tracks favorite models and recently selected models for the user.
/// </summary>
public sealed class ChatModelPreferencesStore
{
    private static readonly Lazy<ChatModelPreferencesStore> s_instance = new(() => new ChatModelPreferencesStore());
    public static ChatModelPreferencesStore Instance => s_instance.Value;

    private readonly object _lock = new();
    private readonly HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _recents = new();
    private readonly string? _filePath;

    public ChatModelPreferencesStore(string? filePath = null)
    {
        try
        {
            if (filePath != null)
            {
                _filePath = filePath;
            }
            else
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var dir = Path.Combine(appData, "OpenClawTray");
                if (Directory.Exists(dir))
                {
                    _filePath = Path.Combine(dir, "model_preferences.json");
                }
            }

            Load();
        }
        catch
        {
            // Fail safe with in-memory persistence
        }
    }

    public bool IsFavorite(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return false;
        lock (_lock)
        {
            return _favorites.Contains(modelId.Trim());
        }
    }

    public bool ToggleFavorite(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return false;
        var clean = modelId.Trim();
        bool isFav;
        lock (_lock)
        {
            if (_favorites.Contains(clean))
            {
                _favorites.Remove(clean);
                isFav = false;
            }
            else
            {
                _favorites.Add(clean);
                isFav = true;
            }
            Save();
        }
        return isFav;
    }

    public void RecordRecent(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return;
        var clean = modelId.Trim();
        lock (_lock)
        {
            _recents.RemoveAll(r => string.Equals(r, clean, StringComparison.OrdinalIgnoreCase));
            _recents.Insert(0, clean);
            if (_recents.Count > 10)
            {
                _recents.RemoveAt(_recents.Count - 1);
            }
            Save();
        }
    }

    public IReadOnlyList<string> GetFavorites()
    {
        lock (_lock)
        {
            return _favorites.ToList();
        }
    }

    public IReadOnlyList<string> GetRecents(int max = 5)
    {
        lock (_lock)
        {
            return _recents.Take(max).ToList();
        }
    }

    private void Load()
    {
        if (_filePath == null || !File.Exists(_filePath)) return;
        try
        {
            var json = File.ReadAllText(_filePath);
            var doc = JsonSerializer.Deserialize<PreferencesPayload>(json);
            if (doc != null)
            {
                if (doc.Favorites != null)
                {
                    foreach (var f in doc.Favorites)
                    {
                        if (!string.IsNullOrWhiteSpace(f)) _favorites.Add(f.Trim());
                    }
                }
                if (doc.Recents != null)
                {
                    foreach (var r in doc.Recents)
                    {
                        if (!string.IsNullOrWhiteSpace(r)) _recents.Add(r.Trim());
                    }
                }
            }
        }
        catch
        {
            // Ignore load errors
        }
    }

    private void Save()
    {
        if (_filePath == null) return;
        try
        {
            var payload = new PreferencesPayload
            {
                Favorites = _favorites.ToList(),
                Recents = _recents.ToList()
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Ignore write errors
        }
    }

    private sealed class PreferencesPayload
    {
        public List<string>? Favorites { get; set; }
        public List<string>? Recents { get; set; }
    }
}
