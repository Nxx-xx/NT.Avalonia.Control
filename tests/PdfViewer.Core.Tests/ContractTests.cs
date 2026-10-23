using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;

namespace PdfViewer.Core.Tests;

public sealed class ContractTests
{
    [Fact]
    public async Task Native_metadata_preserves_Unicode_and_partial_dates_without_claiming_signature_trust()
    {
        await using var document = await PdfDocument.LoadAsync(ContractFixture.Create(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("契约标题", document.Metadata.Title);
        Assert.Equal("Contract author", document.Metadata.Author);
        Assert.Equal("Metadata contract", document.Metadata.Subject);
        Assert.Equal("reader,contract", document.Metadata.Keywords);
        Assert.Equal("Self-authored fixture", document.Metadata.Creator);
        Assert.Equal("NT tests", document.Metadata.Producer);
        Assert.Equal("D:20261015083000+08'00'", document.Metadata.CreationDate);
        Assert.Equal("D:20261015", document.Metadata.ModificationDate);
        Assert.Equal(17, document.Metadata.FileVersion);
        Assert.Equal(0, document.Metadata.SignatureCount);
        Assert.Equal(PdfPasswordAccess.Unencrypted, document.Permissions.PasswordAccess);
        Assert.Equal(uint.MaxValue, document.Permissions.EffectiveFlags);
        Assert.True(document.Capabilities.CanRead);
        Assert.True(document.Capabilities.CanSearch);
        Assert.True(document.Capabilities.CanCopy);
        Assert.False(document.Capabilities.Annotate.IsImplemented);
        Assert.False(document.Capabilities.FillForms.IsImplemented);
        Assert.False(document.Capabilities.Save.IsImplemented);
        Assert.False(document.Capabilities.Print.IsImplemented);
        Assert.False(document.Capabilities.Share.IsImplemented);

        await using var signed = await PdfDocument.LoadAsync(ContractFixture.Create(signature: true), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, signed.Metadata.SignatureCount); // Presence only: fixture contains a deliberately invalid CMS.
        Assert.False(signed.Capabilities.CanSave);
    }

    [Fact]
    public async Task Native_user_and_owner_permissions_are_distinct_and_restricted_text_extraction_is_refused()
    {
        var source = PdfDocumentSource.FromBytes(ContractFixture.Create(restricted: true));
        await using var user = await PdfDocument.LoadAsync(source, "contract-user", TestContext.Current.CancellationToken);
        await using var owner = await PdfDocument.LoadAsync(source, "contract-owner", TestContext.Current.CancellationToken);
        Assert.True(user.Permissions.IsEncrypted);
        Assert.Equal(2, user.Permissions.SecurityHandlerRevision);
        Assert.Equal(PdfPasswordAccess.User, user.Permissions.PasswordAccess);
        Assert.False(user.Permissions.CanCopy);
        Assert.False(user.Permissions.CanPrint);
        Assert.False(user.Permissions.CanPrintHighQuality);
        Assert.False(user.Permissions.CanModify);
        Assert.False(user.Permissions.CanAnnotate);
        Assert.False(user.Permissions.CanFillForms);
        Assert.False(user.Capabilities.CanCopy);
        Assert.Equal(PdfCapabilityUnavailableReason.DocumentPermission, user.Capabilities.Copy.Reason);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => user.GetTextAsync(0, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => user.GetFullTextAsync(TestContext.Current.CancellationToken));
        Assert.NotEmpty(await user.SearchAsync("needle", cancellationToken: TestContext.Current.CancellationToken));
        Assert.NotEmpty((await user.RenderAsync(0, 100, 150, cancellationToken: TestContext.Current.CancellationToken)).Pixels);
        Assert.Equal(PdfPasswordAccess.Owner, owner.Permissions.PasswordAccess);
        Assert.Equal(0xfffffffcu, owner.Permissions.EffectiveFlags);
        Assert.Equal(user.Permissions.UserFlags, owner.Permissions.UserFlags);
        Assert.True(owner.Permissions.CanCopy);
        Assert.True(owner.Permissions.CanPrint);
        Assert.Contains("Contract needle text", (await owner.GetTextAsync(0, TestContext.Current.CancellationToken)).Text);
    }

    [Fact]
    public async Task All_permission_user_and_owner_passwords_are_reported_as_ambiguous_instead_of_guessing_owner()
    {
        var source = PdfDocumentSource.FromBytes(ContractFixture.Create(restricted: true, allowAllPermissions: true));
        await using var user = await PdfDocument.LoadAsync(source, "contract-user", TestContext.Current.CancellationToken);
        await using var owner = await PdfDocument.LoadAsync(source, "contract-owner", TestContext.Current.CancellationToken);
        Assert.Equal(0xfffffffcu, user.Permissions.EffectiveFlags);
        Assert.Equal(user.Permissions.EffectiveFlags, user.Permissions.UserFlags);
        Assert.Equal(user.Permissions, owner.Permissions);
        Assert.Equal(PdfPasswordAccess.UserOrOwner, user.Permissions.PasswordAccess);
        Assert.Equal(PdfPasswordAccess.UserOrOwner, owner.Permissions.PasswordAccess);
        Assert.True(user.Capabilities.CanCopy);
        Assert.True(owner.Capabilities.CanCopy);
    }

    [Fact]
    public async Task Byte_source_captures_ownership_and_reopens_after_input_is_overwritten()
    {
        var bytes = ContractFixture.Create();
        var source = PdfDocumentSource.FromBytes(bytes, "Captured document");
        Array.Fill(bytes, (byte)0);
        Assert.Equal(PdfDocumentSourceKind.Bytes, source.Kind);
        Assert.Null(source.FilePath);
        Assert.Equal("Captured document", source.DisplayName);
        await using var first = await PdfDocument.LoadAsync(source, cancellationToken: TestContext.Current.CancellationToken);
        await using var second = await PdfDocument.LoadAsync(source, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(first.Metadata.Title, second.Metadata.Title);
        Assert.Contains("needle", (await second.GetTextAsync(0, TestContext.Current.CancellationToken)).Text);
    }

    [Fact]
    public async Task Factory_source_owns_each_stream_and_reads_from_its_current_position()
    {
        var streams = new List<TrackingStream>();
        var bytes = new byte[] { 1, 2, 3 }.Concat(ContractFixture.Create()).ToArray();
        var source = PdfDocumentSource.FromStreamFactory(_ =>
        {
            var stream = new TrackingStream(bytes) { Position = 3 };
            streams.Add(stream);
            return ValueTask.FromResult<Stream>(stream);
        });
        await using var first = await PdfDocument.LoadAsync(source, cancellationToken: TestContext.Current.CancellationToken);
        await using var second = await PdfDocument.LoadAsync(source, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, streams.Count);
        Assert.All(streams, stream => Assert.True(stream.WasDisposed));
        Assert.Equal(first.Metadata, second.Metadata);
        var invalid = new TrackingStream([1, 2, 3]);
        await Assert.ThrowsAsync<PdfDocumentException>(() => PdfDocument.LoadAsync(
            PdfDocumentSource.FromStreamFactory(_ => ValueTask.FromResult<Stream>(invalid)), cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(invalid.WasDisposed);
    }

    [Fact]
    public async Task Factory_cancellation_disposes_returned_stream_and_pre_cancellation_does_not_invoke_factory()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var called = false;
        var notCalled = PdfDocumentSource.FromStreamFactory(_ => { called = true; return ValueTask.FromResult<Stream>(new MemoryStream()); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfDocument.LoadAsync(notCalled, cancellationToken: cancellation.Token));
        Assert.False(called);
        var canceledStream = new CancelingStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PdfDocument.LoadAsync(
            PdfDocumentSource.FromStreamFactory(_ => ValueTask.FromResult<Stream>(canceledStream)), cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(canceledStream.WasDisposed);
    }

    [Fact]
    public async Task File_source_is_local_absolute_reopenable_and_reports_access_failure_separately()
    {
        var path = Path.Combine(Path.GetTempPath(), "nt-pdf-contract-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            await File.WriteAllBytesAsync(path, ContractFixture.Create(), TestContext.Current.CancellationToken);
            var source = PdfDocumentSource.FromFile(path);
            Assert.Equal(Path.GetFullPath(path), source.FilePath);
            Assert.Equal(Path.GetFileName(path), source.DisplayName);
            await using var document = await PdfDocument.LoadAsync(source, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("契约标题", document.Metadata.Title);
        }
        finally { File.Delete(path); }
        var error = await Assert.ThrowsAsync<FileNotFoundException>(() => PdfDocument.LoadAsync(PdfDocumentSource.FromFile(path), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(PdfErrorKind.FileAccess, PdfError.FromException(error).Kind);
    }

    [Theory]
    [InlineData(2u, PdfErrorKind.FileAccess)]
    [InlineData(3u, PdfErrorKind.InvalidDocument)]
    [InlineData(4u, PdfErrorKind.PasswordRequiredOrIncorrect)]
    [InlineData(5u, PdfErrorKind.UnsupportedSecurity)]
    [InlineData(6u, PdfErrorKind.PageContent)]
    public void Legacy_native_error_codes_keep_their_values_and_gain_stable_classification(uint code, PdfErrorKind kind)
    {
        var original = new PdfDocumentException("fixture", code);
        var classified = PdfError.FromException(original);
        Assert.Equal(code, original.ErrorCode);
        Assert.Equal(kind, classified.Kind);
        Assert.Equal(code, classified.NativeErrorCode);
        Assert.Same(original, classified.Exception);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Runtime_load_wrappers_keep_native_runtime_classification(int kind)
    {
        Exception cause = kind switch
        {
            0 => new DllNotFoundException("native fixture"),
            1 => new BadImageFormatException("native fixture"),
            _ => new EntryPointNotFoundException("native fixture")
        };
        var wrapped = new PlatformNotSupportedException("Unavailable native runtime.", cause);
        var error = PdfError.FromException(wrapped);
        Assert.Equal(PdfErrorKind.NativeRuntime, error.Kind);
        Assert.Same(wrapped, error.Exception);
        Assert.Null(error.NativeErrorCode);
        Assert.Equal(PdfErrorKind.UnsupportedOperation, PdfError.FromException(new NotSupportedException("feature fixture")).Kind);
    }

    private class TrackingStream(byte[] bytes) : MemoryStream(bytes)
    {
        internal bool WasDisposed { get; private set; }
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
    }

    private sealed class CancelingStream() : TrackingStream([])
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new OperationCanceledException());
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
            Task.FromException(new OperationCanceledException());
    }
}
