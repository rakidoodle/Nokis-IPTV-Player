using Microsoft.Win32;

namespace MyIPTV.App.Services;

public sealed class PlaylistFilePicker : IPlaylistFilePicker
{
    public string? PickPlaylistFile()
    {
        OpenFileDialog dialog = new()
        {
            Title = "Choose an M3U playlist",
            Filter = "M3U playlists (*.m3u;*.m3u8)|*.m3u;*.m3u8|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
