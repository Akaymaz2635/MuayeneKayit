using MuayeneKayit.Core;
using System.Diagnostics;
using MuayeneKayit.Presentation;

namespace MuayeneKayit.Inspector;

/// <summary>
/// Kamera ve fotoğraf düzenleyici pencerelerini açar. Aynı anda tek kamera ve tek düzenleyici penceresi olur:
/// kamera tek uygulamaya açılabilir. Ana pencere kapanınca açık pencereler de kapatılır.
/// </summary>
public sealed class WinUiWindowService : IWindowService
{
    private CameraWindow? _camera;
    private PhotoEditorWindow? _editor;

    public void OpenCamera(CameraRequest request, Func<CapturedPhotoData, string> onSave, Action<string?> onClosed)
    {
        // Aynı anda tek kamera penceresi: kamera tek uygulamaya açılabilir.
        _camera?.Close();

        string? saved = null;
        var camera = new CameraWindow(request.Target, request.DefectId, photo =>
        {
            var name = onSave(photo);
            saved = name;
            return name;
        });
        camera.Closed += (_, _) =>
        {
            if (ReferenceEquals(_camera, camera))
                _camera = null;
            onClosed(saved);
        };
        _camera = camera;
        camera.Activate();
    }

    public void OpenPhotoEditor(PhotoEditorRequest request, Action<PhotoEditResultData> onSave)
    {
        _editor?.Close();
        var editor = new PhotoEditorWindow(
            request.Title, request.DefectsDir, request.Photo, request.Existing, request.Labels, request.CurrentId, onSave);
        editor.Closed += (_, _) =>
        {
            if (ReferenceEquals(_editor, editor))
                _editor = null;
        };
        _editor = editor;
        editor.Activate();
    }

    public void OpenFile(string path) => EdgeBrowser.OpenFile(path);

    /// <summary>Ana pencere kapanınca açık kamera ve düzenleyici kapatılır (kamera serbest kalır).</summary>
    public void CloseAll()
    {
        _camera?.Close();
        _editor?.Close();
    }
}
