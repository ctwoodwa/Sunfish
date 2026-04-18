using Microsoft.JSInterop;

namespace Sunfish.KitchenSink.Services;

public class FavoriteItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Icon { get; set; }
    public string AddedAt { get; set; } = "";
}

public class FavoritesService : IAsyncDisposable
{
    private const string StorageKey = "app:favorites";
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;
    private List<FavoriteItem> _favorites = new();
    private bool _initialized;

    public event Action? OnChanged;

    public IReadOnlyList<FavoriteItem> Favorites => _favorites;

    public FavoritesService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            _module = await _js.InvokeAsync<IJSObjectReference>(
                "import", "./js/favorites.js");

            var json = await _module.InvokeAsync<string?>("getFavorites", StorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    _favorites = System.Text.Json.JsonSerializer.Deserialize<List<FavoriteItem>>(json)
                        ?? new List<FavoriteItem>();
                }
                catch
                {
                    _favorites = new List<FavoriteItem>();
                }
            }
        }
        catch (TaskCanceledException)
        {
            // JS interop was canceled (circuit disconnected or prerendering) — reset so next call retries
            _initialized = false;
        }
        catch (JSDisconnectedException)
        {
            _initialized = false;
        }
    }

    public bool IsFavorite(string id) =>
        _favorites.Any(f => f.Id == id);

    public async Task ToggleAsync(string id, string title, string url, string? icon = null)
    {
        await EnsureInitializedAsync();

        if (IsFavorite(id))
        {
            _favorites.RemoveAll(f => f.Id == id);
        }
        else
        {
            _favorites.Insert(0, new FavoriteItem
            {
                Id = id,
                Title = title,
                Url = url,
                Icon = icon,
                AddedAt = DateTime.UtcNow.ToString("o")
            });
        }

        await SaveAsync();
        OnChanged?.Invoke();
    }

    public async Task RemoveAsync(string id)
    {
        await EnsureInitializedAsync();
        _favorites.RemoveAll(f => f.Id == id);
        await SaveAsync();
        OnChanged?.Invoke();
    }

    private async Task SaveAsync()
    {
        if (_module is null) return;
        var json = System.Text.Json.JsonSerializer.Serialize(_favorites);
        await _module.InvokeVoidAsync("setFavorites", StorageKey, json);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit already disconnected — safe to ignore
            }
        }
    }
}
