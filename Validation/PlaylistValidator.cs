using MusicPlayer.Interfaces;

namespace MusicPlayer.Validation;

public static class PlaylistValidator
{
    public const int MaxNameLength = 64;
    public const int MaxGenreLength = 64;
    public const int MaxTagsLength = 200;

    public static (bool IsValid, string ErrorMessage) ValidateAllFields(
        string? name,
        string? genre,
        string? tags,
        ISettingsService settings,
        int? excludePlaylistId = null)
    {
        if (!StringValidator.HasValueWithContent(name))
            return (false, "Playlist name is required.");

        var trimmedName = name!.Trim();

        if (trimmedName.Length > MaxNameLength)
            return (false, $"Playlist name cannot exceed {MaxNameLength} characters.");

        if (genre is { Length: > MaxGenreLength })
            return (false, $"Genre cannot exceed {MaxGenreLength} characters.");

        if (tags is { Length: > MaxTagsLength })
            return (false, $"Tags cannot exceed {MaxTagsLength} characters.");

        var nameExists = settings.GetAllPlaylists().Any(p =>
            p.Id != excludePlaylistId &&
            string.Equals(p.Name, trimmedName, StringComparison.OrdinalIgnoreCase));

        if (nameExists)
            return (false, $"A playlist named \"{trimmedName}\" already exists.");

        return (true, string.Empty);
    }
}
