using System.IO.MemoryMappedFiles;

namespace VCamNetSampleSourceAOT;

internal sealed class SharedFrameReader : IDisposable
{
    private const int HeaderSize = 64;
    private MemoryMappedFile? _mapping;
    private MemoryMappedViewAccessor? _view;
    private byte[] _source = [];
    private byte[] _target = [];
    private int _waitingWidth;
    private int _waitingHeight;

    public byte[] ReadScaledBgra(int targetWidth, int targetHeight)
    {
        if (!TryOpen() || !TryRead(out var width, out var height, out var stride, out var bytes))
        {
            var waiting = EnsureTarget(targetWidth, targetHeight);
            if (_waitingWidth != targetWidth || _waitingHeight != targetHeight)
            {
                FillWaitingFrame(waiting, targetWidth, targetHeight);
                _waitingWidth = targetWidth;
                _waitingHeight = targetHeight;
            }
            return waiting;
        }

        _waitingWidth = _waitingHeight = 0;
        if (width == targetWidth && height == targetHeight && stride == targetWidth * 4) return bytes;

        var target = EnsureTarget(targetWidth, targetHeight);

        var scale = Math.Min(targetWidth / (double)width, targetHeight / (double)height);
        var drawWidth = Math.Max(1, (int)Math.Round(width * scale));
        var drawHeight = Math.Max(1, (int)Math.Round(height * scale));
        var offsetX = (targetWidth - drawWidth) / 2;
        var offsetY = (targetHeight - drawHeight) / 2;
        if (offsetX != 0 || offsetY != 0) Array.Clear(target);

        if (width == drawWidth && height == drawHeight)
        {
            var rowBytes = width * 4;
            for (var y = 0; y < height; y++)
            {
                Buffer.BlockCopy(bytes, y * stride, target, ((y + offsetY) * targetWidth + offsetX) * 4, rowBytes);
            }
            return target;
        }

        for (var y = 0; y < drawHeight; y++)
        {
            var sourceY = Math.Min(height - 1, y * height / drawHeight);
            for (var x = 0; x < drawWidth; x++)
            {
                var sourceX = Math.Min(width - 1, x * width / drawWidth);
                var sourceOffset = (sourceY * stride) + (sourceX * 4);
                var targetOffset = (((y + offsetY) * targetWidth) + x + offsetX) * 4;
                target[targetOffset] = bytes[sourceOffset];
                target[targetOffset + 1] = bytes[sourceOffset + 1];
                target[targetOffset + 2] = bytes[sourceOffset + 2];
                target[targetOffset + 3] = 0xff;
            }
        }
        return target;
    }

    private byte[] EnsureTarget(int width, int height)
    {
        var required = checked(width * height * 4);
        if (_target.Length != required) _target = new byte[required];
        return _target;
    }

    private bool TryOpen()
    {
        if (_view is not null) return true;
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PocketCam", "frames.v1");
            if (!File.Exists(path)) return false;
            _mapping = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            _view = _mapping.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            return true;
        }
        catch
        {
            CloseMapping();
            return false;
        }
    }

    private bool TryRead(out int width, out int height, out int stride, out byte[] bytes)
    {
        width = height = stride = 0;
        bytes = [];
        try
        {
            if (_view!.ReadByte(0) != (byte)'P' || _view.ReadByte(1) != (byte)'C' ||
                _view.ReadByte(2) != (byte)'V' || _view.ReadByte(3) != (byte)'F') return false;
            var sequenceBefore = _view.ReadInt32(12);
            if ((sequenceBefore & 1) != 0) return false;
            width = _view.ReadInt32(16);
            height = _view.ReadInt32(20);
            stride = _view.ReadInt32(24);
            var length = _view.ReadInt32(28);
            if (width is <= 0 or > 3840 || height is <= 0 or > 2160 || stride < width * 4 ||
                length != stride * height || length > 3840 * 2160 * 4) return false;
            if (_source.Length != length) _source = new byte[length];
            _view.ReadArray(HeaderSize, _source, 0, length);
            var sequenceAfter = _view.ReadInt32(12);
            if (sequenceBefore != sequenceAfter || (sequenceAfter & 1) != 0) return false;
            bytes = _source;
            return true;
        }
        catch
        {
            CloseMapping();
            return false;
        }
    }

    private static void FillWaitingFrame(byte[] frame, int width, int height)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = ((y * width) + x) * 4;
                frame[offset] = 0x1d;
                frame[offset + 1] = (byte)(0x1a + (x * 0x18 / width));
                frame[offset + 2] = 0x07;
                frame[offset + 3] = 0xff;
            }
        }
    }

    private void CloseMapping()
    {
        _view?.Dispose();
        _mapping?.Dispose();
        _view = null;
        _mapping = null;
    }

    public void Dispose() => CloseMapping();
}
