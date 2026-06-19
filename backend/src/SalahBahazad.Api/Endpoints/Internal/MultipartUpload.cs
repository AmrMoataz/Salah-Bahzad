using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace SalahBahazad.Api.Endpoints.Internal;

/// <summary>
/// Helpers for streaming multipart uploads. Video endpoints parse the request with a
/// <see cref="MultipartReader"/> instead of binding <c>IFormFile</c> — the latter buffers the whole file
/// to a temp file on the app server's disk (docs/05 §6), which is unacceptable for multi-GB sources. The
/// reader hands the live file-section stream straight to R2 as the bytes arrive.
/// </summary>
internal static class MultipartUpload
{
    public static bool IsMultipart(string? contentType)
        => !string.IsNullOrEmpty(contentType)
           && contentType.Contains("multipart/", StringComparison.OrdinalIgnoreCase);

    /// <summary>Extracts the multipart boundary from the Content-Type, or throws a 400-mapped error.</summary>
    public static string GetBoundary(string contentType)
    {
        var boundary = HeaderUtilities.RemoveQuotes(
            MediaTypeHeaderValue.Parse(contentType).Boundary).Value;

        if (string.IsNullOrWhiteSpace(boundary))
            throw new InvalidDataException("Missing multipart boundary.");

        return boundary;
    }

    /// <summary>A section is the uploaded file when its content-disposition carries a filename.</summary>
    public static bool IsFile(ContentDispositionHeaderValue disposition)
        => disposition.DispositionType.Equals("form-data")
           && (!string.IsNullOrEmpty(disposition.FileName.Value)
               || !string.IsNullOrEmpty(disposition.FileNameStar.Value));

    /// <summary>A section is a plain text field when it is form-data without a filename.</summary>
    public static bool IsFormField(ContentDispositionHeaderValue disposition)
        => disposition.DispositionType.Equals("form-data")
           && string.IsNullOrEmpty(disposition.FileName.Value)
           && string.IsNullOrEmpty(disposition.FileNameStar.Value);

    /// <summary>Reads a (small) text field's value.</summary>
    public static async Task<string> ReadFieldValueAsync(
        MultipartSection section, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            section.Body, System.Text.Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        return (await reader.ReadToEndAsync(cancellationToken)).Trim();
    }
}

/// <summary>
/// A forward-only read wrapper that aborts once more than <paramref name="limit"/> bytes have been read,
/// enforcing the source-size cap mid-stream (the backstop to the Content-Length pre-check, for the rare
/// chunked request where the length isn't known up front). Throws <see cref="InvalidDataException"/>,
/// mapped to <c>400</c> by the global exception handler.
/// </summary>
internal sealed class LengthLimitingStream(Stream inner, long limit) : Stream
{
    private long _read;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => _read; set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = inner.Read(buffer, offset, count);
        Track(n);
        return n;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var n = await inner.ReadAsync(buffer, cancellationToken);
        Track(n);
        return n;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private void Track(int n)
    {
        _read += n;
        if (_read > limit)
            throw new InvalidDataException("The video exceeds the 2 GB limit.");
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
