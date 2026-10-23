using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace PdfViewer.P0;

/// <summary>Bounded native host matrix; this is not an Avalonia form editing API.</summary>
internal static class AdvancedFormsProbe
{
    public static object Run(string runDirectory)
    {
        Directory.CreateDirectory(runDirectory);
        string inputPath = Path.Combine(runDirectory, "input.pdf");
        string firstPath = Path.Combine(runDirectory, "focused-saved.pdf");
        string secondPath = Path.Combine(runDirectory, "second-saved.pdf");
        byte[] input = CreateFixture();
        File.WriteAllBytes(inputPath, input);
        var checks = new List<ProbeCheck>();
        var events = new List<object>();
        FieldState[] before, first, reopened, second, reopenedSecond;
        CallbackStatistics callbacks;
        using (var host = new FormHost(input))
        {
            before = host.ReadFields();
            checks.Add(Check("widget-type-matrix", before.Select(x => x.Type).SequenceEqual([3, 3, 6, 5, 5, 6, 6]),
                string.Join(',', before.Select(x => x.Type))));
            checks.Add(Check("native-readonly-required-flags", before.Single(x => x.Name == "ReadOnly").Flags == 1 &&
                before.Single(x => x.Name == "Required").Flags == 2, "ReadOnly bit0 and Required bit1 preserved."));
            events.Add(host.FocusAndClick("radio-Blue", 110, 600));
            Native.FORM_ForceToKillFocus(host.Handle);
            events.Add(host.FocusAndClick("multiline", 60, 520));
            host.Type("first line\r\nsecond line");
            events.Add(new { Operation = "multiline-focused-text", Input = "first line\r\nsecond line", Translation = "CRLF is one Return keydown plus translated CR character", Text = host.ReadFocusedText() });
            Native.FORM_ForceToKillFocus(host.Handle);
            events.Add(host.FocusAndClick("single-list", 80, 390));
            events.Add(new { Operation = "single-list-key-down", NativeResult = Native.FORM_OnKeyDown(host.Handle, host.Page, 0x28, 0) });
            Native.FORM_ForceToKillFocus(host.Handle);
            events.Add(host.FocusAndClick("multi-list-row0", 340, 390));
            events.Add(Click(host, "multi-list-control-row2", 340, 366, 2));
            Native.FORM_ForceToKillFocus(host.Handle);
            events.Add(host.FocusAndClick("readonly", 80, 215));
            host.Type("MUST NOT WRITE");
            Native.FORM_ForceToKillFocus(host.Handle);
            events.Add(host.FocusAndClick("required", 80, 155));
            host.Type("provided");
            Native.FORM_ForceToKillFocus(host.Handle);
            first = host.ReadFields();
            AddExpected(checks, "first-commit", first, "Blue", "first line\r\nsecond line", "G", [1], "R", [0, 2], "provided");
            checks.Add(Check("readonly-input-refused", first.Single(x => x.Name == "ReadOnly").Value == "FIXED RO", "Typing at read-only widget leaves canonical value unchanged."));
            host.Save(firstPath);
            callbacks = host.Statistics;
        }
        using (var host = new FormHost(File.ReadAllBytes(firstPath)))
        {
            reopened = host.ReadFields();
            AddExpected(checks, "first-reopen", reopened, "Blue", "first line\r\nsecond line", "G", [1], "R", [0, 2], "provided");
            checks.Add(Check("normal-appearances-after-first-save", reopened.All(x => x.HasAppearance), "Every widget retains its normal appearance; radio AP uses state dictionaries."));
            events.Add(host.FocusAndClick("second-radio-Red", 50, 600));
            Native.FORM_ForceToKillFocus(host.Handle);
            events.Add(host.FocusAndClick("second-multiline", 60, 520));
            host.ReplaceText("changed line\rfinal line");
            events.Add(host.FocusAndClick("second-single-list", 80, 390));
            Native.FORM_OnKeyDown(host.Handle, host.Page, 0x23, 0);
            Native.FORM_ForceToKillFocus(host.Handle);
            events.Add(host.FocusAndClick("second-multi-list-row1", 340, 378));
            events.Add(Click(host, "second-multi-list-control-row3", 340, 354, 2));
            Native.FORM_ForceToKillFocus(host.Handle);
            events.Add(host.FocusAndClick("second-required-empty", 80, 155));
            host.ReplaceText("");
            // Ctrl+A + Backspace routes native deletion; empty Type() alone cannot remove selection.
            Native.FORM_OnFocus(host.Handle, host.Page, 0, 80, 155);
            Native.FORM_OnKeyDown(host.Handle, host.Page, 0x41, 2);
            Native.FORM_OnChar(host.Handle, host.Page, 0x08, 0);
            Native.FORM_ForceToKillFocus(host.Handle);
            second = host.ReadFields();
            AddExpected(checks, "second-commit", second, "Red", "changed line\r\nfinal line", "K", [3], "G", [1, 3], "");
            checks.Add(Check("required-does-not-validate-automatically", second.Single(x => x.Name == "Required").Value == "", "Native editing permits an empty required field; application submission validation remains separate."));
            host.Save(secondPath);
        }
        using (var host = new FormHost(File.ReadAllBytes(secondPath)))
        {
            reopenedSecond = host.ReadFields();
            AddExpected(checks, "second-reopen", reopenedSecond, "Red", "changed line\r\nfinal line", "K", [3], "G", [1, 3], "");
            checks.Add(Check("flags-and-widget-count-after-second-save", reopenedSecond.Length == 7 &&
                reopenedSecond.Single(x => x.Name == "Multiline").Flags == 4096 &&
                reopenedSecond.Single(x => x.Name == "MultiList").Flags == 2097152 &&
                reopenedSecond.Single(x => x.Name == "ReadOnly").Flags == 1 &&
                reopenedSecond.Single(x => x.Name == "Required").Flags == 2, "All widget flags survive second native focus/input/blur/save/reopen."));
        }
        return new
        {
            Id = "POC-03-advanced", Status = checks.All(x => x.Passed) ? "bounded-native-host-verified" : "failed",
            Description = "Radio parent/kids, multiline translated Return, list export/display options, read-only and required flags through native focus/mouse/key/character/blur, two full saves and reopen.",
            FixtureOwnership = "Hand-authored by AdvancedFormsProbe; no third-party document data.",
            Outputs = new[] { new { File = "input.pdf", SHA256 = Hash(inputPath) }, new { File = "focused-saved.pdf", SHA256 = Hash(firstPath) }, new { File = "second-saved.pdf", SHA256 = Hash(secondPath) } },
            Before = before, AfterCommit = first, AfterReopen = reopened, AfterSecondCommit = second, AfterSecondReopen = reopenedSecond,
            Checks = checks, Events = events, Callbacks = callbacks,
            Limitations = new[]
            {
                "Native PDFium host experiment only. No production form session, Avalonia keyboard/pointer/focus routing, accessibility, dispatcher timer loop or physical IME acceptance.",
                "Required is a persisted field flag; this probe demonstrates that native editing does not itself enforce submit-time required validation.",
                "JavaScript, validation/calculation actions, XFA, signatures, encrypted input and arbitrary appearance styling are outside this matrix.",
                "Independent pypdf canonical tree/values/options/appearances and Poppler rendering are required in the surrounding workflow."
            }
        };
    }

    private static object Click(FormHost host, string target, double x, double y, int modifier)
    {
        int down = Native.FORM_OnLButtonDown(host.Handle, host.Page, modifier, x, y);
        int up = Native.FORM_OnLButtonUp(host.Handle, host.Page, modifier, x, y);
        return new { Operation = "mouse-click", Target = target, X = x, Y = y, Modifier = modifier, DownResult = down, UpResult = up };
    }

    private static void AddExpected(List<ProbeCheck> checks, string prefix, FieldState[] fields, string radio,
        string multiline, string single, int[] singleIndices, string multiFirstValue, int[] multiIndices, string required)
    {
        FieldState[] radios = fields.Where(x => x.Name == "Radio").ToArray();
        checks.Add(Check(prefix + "-radio-exclusive", radios.Length == 2 && radios.All(x => x.Value == radio) &&
            radios.Count(x => x.Checked) == 1 && radios[radio == "Red" ? 0 : 1].Checked,
            string.Join(";", radios.Select(x => $"{x.Value}:checked={x.Checked}"))));
        FieldState text = fields.Single(x => x.Name == "Multiline");
        checks.Add(Check(prefix + "-multiline-value", text.Value == multiline, $"Value={text.Value.Replace("\r", "<CR>").Replace("\n", "<LF>")}"));
        FieldState list = fields.Single(x => x.Name == "SingleList");
        checks.Add(Check(prefix + "-single-list-export-index", list.Value == single && list.SelectedIndices.SequenceEqual(singleIndices),
            $"/V={list.Value}; selected=[{string.Join(',', list.SelectedIndices)}]"));
        FieldState multi = fields.Single(x => x.Name == "MultiList");
        checks.Add(Check(prefix + "-multi-list-exports-indices", multi.Value == multiFirstValue && multi.SelectedIndices.SequenceEqual(multiIndices),
            $"Native first /V={multi.Value}; selected=[{string.Join(',', multi.SelectedIndices)}]; canonical array independently checked."));
        checks.Add(Check(prefix + "-required-value", fields.Single(x => x.Name == "Required").Value == required, $"Required /V={fields.Single(x => x.Name == "Required").Value}"));
    }
    private sealed record ProbeCheck(string Id, bool Passed, string Detail);
    private static ProbeCheck Check(string id, bool passed, string detail) => new(id, passed, detail);
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private sealed record FieldState(string Name, int Type, string Value, bool Checked, int[] SelectedIndices, bool HasAppearance, ulong AppearanceUtf16Bytes, int Flags);
    private sealed record CallbackStatistics(int Invalidations, int Changes, int FocusNotifications, int TimersCreated, int TimersKilled);

    private sealed class FormHost : IDisposable
    {
        private readonly GCHandle _pin;
        private readonly IntPtr _info;
        private readonly List<Delegate> _callbacks = [];
        private int _invalidations;
        private int _changes;
        private int _focusNotifications;
        private int _timersCreated;
        private int _timersKilled;
        public IntPtr Document { get; }
        public IntPtr Page { get; }
        public IntPtr Handle { get; }
        public CallbackStatistics Statistics => new(_invalidations, _changes, _focusNotifications, _timersCreated, _timersKilled);

        public FormHost(byte[] input)
        {
            _pin = GCHandle.Alloc(input, GCHandleType.Pinned);
            Document = Native.FPDF_LoadMemDocument64(_pin.AddrOfPinnedObject(), (nuint)input.Length, null);
            if (Document == IntPtr.Zero)
            {
                _pin.Free();
                throw new InvalidOperationException("Cannot load generated AcroForm fixture.");
            }
            Page = Native.FPDF_LoadPage(Document, 0);
            if (Page == IntPtr.Zero)
            {
                Native.FPDF_CloseDocument(Document);
                _pin.Free();
                throw new InvalidOperationException("Cannot load AcroForm page.");
            }

            // Callbacks are ordinary C function pointers in fpdf_formfill.h, unlike exported FPDF_CALLCONV APIs.
            var info = new FormFillInfo
            {
                Version = 1,
                Release = IntPtr.Zero,
                Invalidate = Pin(new Native.Invalidate((_, _, _, _, _, _) => _invalidations++)),
                OutputSelectedRect = IntPtr.Zero,
                SetCursor = Pin(new Native.SetCursor((_, _) => { })),
                SetTimer = Pin(new Native.SetTimer((_, _, _) => ++_timersCreated)),
                KillTimer = Pin(new Native.KillTimer((_, _) => _timersKilled++)),
                GetLocalTime = IntPtr.Zero, // Documented unused in the matching public header.
                OnChange = Pin(new Native.OnChange(_ => _changes++)),
                GetPage = Pin(new Native.GetPage((_, _, index) => index == 0 ? Page : IntPtr.Zero)),
                GetCurrentPage = Pin(new Native.GetCurrentPage((_, _) => Page)),
                GetRotation = Pin(new Native.GetRotation((_, _) => 0)),
                ExecuteNamedAction = Pin(new Native.NamedAction((_, _) => { })),
                SetTextFieldFocus = Pin(new Native.SetTextFieldFocus((_, _, _, _) => _focusNotifications++)),
                DoUriAction = IntPtr.Zero,
                DoGoToAction = IntPtr.Zero,
                JsPlatform = IntPtr.Zero // No script engine is enabled by this probe.
            };
            // Keep a zero-initialized tail so unused version 2 members cannot contain random addresses.
            _info = Marshal.AllocHGlobal(512);
            Marshal.Copy(new byte[512], 0, _info, 512);
            Marshal.StructureToPtr(info, _info, false);
            Handle = Native.FPDFDOC_InitFormFillEnvironment(Document, _info);
            if (Handle == IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_info);
                Native.FPDF_ClosePage(Page);
                Native.FPDF_CloseDocument(Document);
                _pin.Free();
                throw new InvalidOperationException("FPDFDOC_InitFormFillEnvironment failed.");
            }
            Native.FORM_OnAfterLoadPage(Page, Handle);
        }

        private IntPtr Pin(Delegate callback)
        {
            _callbacks.Add(callback);
            return Marshal.GetFunctionPointerForDelegate(callback);
        }

        public object FocusAndClick(string target, double x, double y)
        {
            int focus = Native.FORM_OnFocus(Handle, Page, 0, x, y);
            int down = Native.FORM_OnLButtonDown(Handle, Page, 0, x, y);
            int up = Native.FORM_OnLButtonUp(Handle, Page, 0, x, y);
            return new { Operation = "focus-and-mouse-click", Target = target, X = x, Y = y, FocusResult = focus, DownResult = down, UpResult = up };
        }

        public string ReadFocusedText() => ReadUtf16((buffer, length) => Native.FORM_GetFocusedText(Handle, Page, buffer, length));

        public FieldState[] ReadFields()
        {
            int count = Native.FPDFPage_GetAnnotCount(Page);
            var fields = new List<FieldState>();
            for (int index = 0; index < count; index++)
            {
                IntPtr annot = Native.FPDFPage_GetAnnot(Page, index);
                try
                {
                    string name = ReadUtf16((buffer, length) => Native.FPDFAnnot_GetFormFieldName(Handle, annot, buffer, length));
                    string value = ReadUtf16((buffer, length) => Native.FPDFAnnot_GetFormFieldValue(Handle, annot, buffer, length));
                    int optionCount = Native.FPDFAnnot_GetOptionCount(Handle, annot);
                    var selected = new List<int>();
                    for (int option = 0; option < optionCount; option++)
                        if (Native.FPDFAnnot_IsOptionSelected(Handle, annot, option) != 0)
                            selected.Add(option);
                    ulong appearanceBytes = Native.FPDFAnnot_GetAP(annot, 0, IntPtr.Zero, new CULong(0u)).Value;
                    fields.Add(new FieldState(name, Native.FPDFAnnot_GetFormFieldType(Handle, annot), value,
                        Native.FPDFAnnot_IsChecked(Handle, annot) != 0, selected.ToArray(), appearanceBytes > 2, appearanceBytes, Native.FPDFAnnot_GetFormFieldFlags(Handle, annot)));
                }
                finally { Native.FPDFPage_CloseAnnot(annot); }
            }
            return fields.ToArray();
        }

        private static string ReadUtf16(Func<IntPtr, CULong, CULong> reader)
        {
            nuint length = reader(IntPtr.Zero, new CULong(0u)).Value;
            if (length < 2) return "";
            IntPtr buffer = Marshal.AllocHGlobal(checked((int)length));
            try
            {
                reader(buffer, new CULong(length));
                return Marshal.PtrToStringUni(buffer, checked((int)length / 2 - 1)) ?? "";
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        public void Type(string text)
        {
            foreach (char character in text.Replace("\r\n", "\r").Replace('\n', '\r'))
            {
                if (character == '\r') Native.FORM_OnKeyDown(Handle, Page, 0x0D, 0);
                Native.FORM_OnChar(Handle, Page, character, 0);
            }
        }

        public void ReplaceText(string text)
        {
            Native.FORM_OnKeyDown(Handle, Page, 0x41, 2);
            Type(text);
            Native.FORM_ForceToKillFocus(Handle);
        }

        public void Save(string outputPath)
        {
            using var stream = new MemoryStream();
            Exception? callbackError = null;
            Native.WriteBlock callback = (_, data, length) =>
            {
                try
                {
                    var bytes = new byte[checked((int)length.Value)];
                    Marshal.Copy(data, bytes, 0, bytes.Length);
                    stream.Write(bytes);
                    return 1;
                }
                catch (Exception exception) { callbackError = exception; return 0; }
            };
            var writer = new FileWrite { Version = 1, WriteBlock = Marshal.GetFunctionPointerForDelegate(callback) };
            int saved = Native.FPDF_SaveAsCopy(Document, ref writer, new CULong(2u));
            GC.KeepAlive(callback);
            if (callbackError is not null) throw new IOException("Native save callback failed.", callbackError);
            if (saved == 0) throw new InvalidOperationException("Native AcroForm save failed.");
            File.WriteAllBytes(outputPath, stream.ToArray());
        }

        public void Dispose()
        {
            Native.FORM_ForceToKillFocus(Handle);
            Native.FORM_OnBeforeClosePage(Page, Handle);
            Native.FPDF_ClosePage(Page);
            Native.FPDFDOC_ExitFormFillEnvironment(Handle);
            Native.FPDF_CloseDocument(Document);
            Marshal.FreeHGlobal(_info);
            _pin.Free();
            GC.KeepAlive(_callbacks);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FormFillInfo
    {
        public int Version;
        public IntPtr Release, Invalidate, OutputSelectedRect, SetCursor, SetTimer, KillTimer, GetLocalTime, OnChange,
            GetPage, GetCurrentPage, GetRotation, ExecuteNamedAction, SetTextFieldFocus, DoUriAction, DoGoToAction, JsPlatform;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileWrite { public int Version; public IntPtr WriteBlock; }

    private static byte[] CreateFixture()
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R /AcroForm 5 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 600 700] /Resources << /Font << /Helv 4 0 R >> >> /Contents 6 0 R /Annots [8 0 R 9 0 R 10 0 R 11 0 R 12 0 R 13 0 R 14 0 R] >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Fields [7 0 R 10 0 R 11 0 R 12 0 R 13 0 R 14 0 R] /DR << /Font << /Helv 4 0 R >> >> /DA (/Helv 12 Tf 0 g) /NeedAppearances false >>",
            Stream("BT /Helv 18 Tf 40 665 Td (P0 advanced AcroForm) Tj ET\nBT /Helv 11 Tf 40 630 Td (Radio parent and child widgets: Red / Blue) Tj ET\nBT /Helv 11 Tf 40 555 Td (Multiline: translated Return character) Tj ET\nBT /Helv 11 Tf 40 415 Td (Single list export/display pairs) Tj ET\nBT /Helv 11 Tf 300 415 Td (Multi list: Ctrl-click) Tj ET\nBT /Helv 11 Tf 40 242 Td (Read-only) Tj ET\nBT /Helv 11 Tf 40 182 Td (Required - no automatic submit validation) Tj ET\nBT /Helv 12 Tf 40 80 Td (ORIGINAL CONTENT 314159) Tj ET"),
            "<< /FT /Btn /T (Radio) /Ff 49152 /V /Off /Kids [8 0 R 9 0 R] >>",
            "<< /Type /Annot /Subtype /Widget /Parent 7 0 R /Rect [40 590 60 610] /F 4 /P 3 0 R /AS /Off /AP << /N << /Off 15 0 R /Red 16 0 R >> >> >>",
            "<< /Type /Annot /Subtype /Widget /Parent 7 0 R /Rect [100 590 120 610] /F 4 /P 3 0 R /AS /Off /AP << /N << /Off 15 0 R /Blue 16 0 R >> >> >>",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /Ff 4096 /T (Multiline) /V () /Rect [40 430 400 540] /F 4 /P 3 0 R /DA (/Helv 12 Tf 0 g) /MK << /BC [0 0 0] /BG [1 1 1] >> /BS << /W 1 /S /S >> /AP << /N 17 0 R >> >>",
            "<< /Type /Annot /Subtype /Widget /FT /Ch /T (SingleList) /V (R) /I [0] /Opt [[(R) (Red display)] [(G) (Green display)] [(B) (Blue display)] [(K) (Black display)]] /Rect [40 270 260 400] /F 4 /P 3 0 R /DA (/Helv 12 Tf 0 g) /MK << /BC [0 0 0] /BG [1 1 1] >> /BS << /W 1 /S /S >> /AP << /N 18 0 R >> >>",
            "<< /Type /Annot /Subtype /Widget /FT /Ch /Ff 2097152 /T (MultiList) /V (R) /I [0] /Opt [[(R) (Red display)] [(G) (Green display)] [(B) (Blue display)] [(K) (Black display)]] /Rect [300 270 560 400] /F 4 /P 3 0 R /DA (/Helv 12 Tf 0 g) /MK << /BC [0 0 0] /BG [1 1 1] >> /BS << /W 1 /S /S >> /AP << /N 19 0 R >> >>",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /Ff 1 /T (ReadOnly) /V (FIXED RO) /Rect [40 200 300 230] /F 4 /P 3 0 R /DA (/Helv 12 Tf 0 g) /MK << /BC [0 0 0] /BG [1 1 1] >> /BS << /W 1 /S /S >> /AP << /N 20 0 R >> >>",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /Ff 2 /T (Required) /V () /Rect [40 140 300 170] /F 4 /P 3 0 R /DA (/Helv 12 Tf 0 g) /MK << /BC [0 0 0] /BG [1 1 1] >> /BS << /W 1 /S /S >> /AP << /N 21 0 R >> >>",
            Appearance(20, 20, "q 1 1 1 rg 0 0 20 20 re f 0 G 1 w 1 1 18 18 re S Q"),
            Appearance(20, 20, "q 1 1 1 rg 0 0 20 20 re f 0 G 1 w 1 1 18 18 re S 0 g 5 5 10 10 re f Q"),
            Appearance(360, 110, "q 1 1 1 rg 0 0 360 110 re f 0 G 1 w 0.5 0.5 359 109 re S Q"),
            ListAppearance(220),
            ListAppearance(260),
            Appearance(260, 30, "q 1 1 1 rg 0 0 260 30 re f 0 G 1 w 0.5 0.5 259 29 re S 0 g BT /Helv 12 Tf 4 10 Td (FIXED RO) Tj ET Q"),
            Appearance(260, 30, "q 1 1 1 rg 0 0 260 30 re f 0 G 1 w 0.5 0.5 259 29 re S Q")
        };
        using var stream = new MemoryStream();
        static void Write(MemoryStream target, string value) => target.Write(Encoding.Latin1.GetBytes(value));
        Write(stream, "%PDF-1.7\n%\xe2\xe3\xcf\xd3\n");
        var offsets = new List<long> { 0 };
        for (int index = 0; index < objects.Count; index++)
        {
            offsets.Add(stream.Position);
            Write(stream, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        long xref = stream.Position;
        Write(stream, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (long offset in offsets.Skip(1)) Write(stream, offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Write(stream, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return stream.ToArray();
    }

    private static string Stream(string content) => $"<< /Length {Encoding.Latin1.GetByteCount(content)} >>\nstream\n{content}\nendstream";
    private static string Appearance(int width, int height, string content) =>
        $"<< /Type /XObject /Subtype /Form /BBox [0 0 {width} {height}] /Resources << /Font << /Helv 4 0 R >> >> /Length {Encoding.Latin1.GetByteCount(content)} >>\nstream\n{content}\nendstream";
    private static string ListAppearance(int width) => Appearance(width, 130,
        $"q 1 1 1 rg 0 0 {width} 130 re f 0 G 1 w 0.5 0.5 {width - 1} 129 re S 0.1 0.3 0.7 rg 1 115 {width - 2} 14 re f 1 g BT /Helv 12 Tf 4 118 Td (Red display) Tj 0 g 0 -14 Td (Green display) Tj 0 -14 Td (Blue display) Tj 0 -14 Td (Black display) Tj ET Q");
    private static class Native
    {
        private const string Library = "pdfium";
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void Invalidate(IntPtr self, IntPtr page, double left, double top, double right, double bottom);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void SetCursor(IntPtr self, int cursor);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int SetTimer(IntPtr self, int milliseconds, IntPtr callback);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void KillTimer(IntPtr self, int id);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void OnChange(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr GetPage(IntPtr self, IntPtr document, int index);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr GetCurrentPage(IntPtr self, IntPtr document);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int GetRotation(IntPtr self, IntPtr page);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void NamedAction(IntPtr self, IntPtr action);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void SetTextFieldFocus(IntPtr self, IntPtr value, CULong length, int focused);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int WriteBlock(IntPtr self, IntPtr data, CULong length);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDF_LoadMemDocument64(IntPtr bytes, nuint length, [MarshalAs(UnmanagedType.LPUTF8Str)] string? password);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDF_LoadPage(IntPtr document, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDF_ClosePage(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDF_CloseDocument(IntPtr document);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDF_GetFormType(IntPtr document);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFDOC_InitFormFillEnvironment(IntPtr document, IntPtr info);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFDOC_ExitFormFillEnvironment(IntPtr form);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FORM_OnAfterLoadPage(IntPtr page, IntPtr form);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FORM_OnBeforeClosePage(IntPtr page, IntPtr form);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FORM_OnFocus(IntPtr form, IntPtr page, int modifier, double x, double y);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FORM_OnLButtonDown(IntPtr form, IntPtr page, int modifier, double x, double y);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FORM_OnLButtonUp(IntPtr form, IntPtr page, int modifier, double x, double y);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FORM_OnChar(IntPtr form, IntPtr page, int character, int modifier);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FORM_OnKeyDown(IntPtr form, IntPtr page, int key, int modifier);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FORM_ForceToKillFocus(IntPtr form);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern CULong FORM_GetFocusedText(IntPtr form, IntPtr page, IntPtr buffer, CULong length);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFPage_GetAnnotCount(IntPtr page);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern IntPtr FPDFPage_GetAnnot(IntPtr page, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern void FPDFPage_CloseAnnot(IntPtr annotation);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern CULong FPDFAnnot_GetFormFieldName(IntPtr form, IntPtr annotation, IntPtr buffer, CULong length);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern CULong FPDFAnnot_GetFormFieldValue(IntPtr form, IntPtr annotation, IntPtr buffer, CULong length);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_GetFormFieldFlags(IntPtr form, IntPtr annotation);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_GetFormFieldType(IntPtr form, IntPtr annotation);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_GetOptionCount(IntPtr form, IntPtr annotation);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_IsOptionSelected(IntPtr form, IntPtr annotation, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_IsChecked(IntPtr form, IntPtr annotation);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern CULong FPDFAnnot_GetAP(IntPtr annotation, int mode, IntPtr buffer, CULong length);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDF_SaveAsCopy(IntPtr document, ref FileWrite writer, CULong flags);
    }
}
