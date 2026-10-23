using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace PdfViewer.P0;

/// <summary>
/// Standalone native form-host experiment. It intentionally does not expose a product editing API.
/// The form dictionary, widgets and appearance streams remain interactive after saving.
/// </summary>
internal static class FormsProbe
{
    public static object Run(string runDirectory)
    {
        Directory.CreateDirectory(runDirectory);
        string inputPath = Path.Combine(runDirectory, "forms-input.pdf");
        string outputPath = Path.Combine(runDirectory, "forms-saved.pdf");
        byte[] input = CreateFixture();
        File.WriteAllBytes(inputPath, input);
        var checks = new List<ProbeCheck>();
        var events = new List<object>();
        const string expectedText = "P0 focused input";
        var before = Array.Empty<FieldState>();
        var after = Array.Empty<FieldState>();
        CallbackStatistics callbacks;
        using (var host = new FormHost(input))
        {
            before = host.ReadFields();
            checks.Add(Check("acroform-detected", Native.FPDF_GetFormType(host.Document) == 1, "FPDF_GetFormType == FORMTYPE_ACRO_FORM"));
            checks.Add(Check("native-host-initialized", host.Handle != IntPtr.Zero, "FPDFDOC_InitFormFillEnvironment(version=1), FORM_OnAfterLoadPage"));

            events.Add(host.FocusAndClick("text", 80, 312));
            int accepted = 0;
            foreach (char character in expectedText)
            {
                if (Native.FORM_OnChar(host.Handle, host.Page, character, 0) != 0)
                    accepted++;
            }
            string focusedText = host.ReadFocusedText();
            checks.Add(Check("text-via-onchar", accepted == expectedText.Length && focusedText == expectedText,
                $"FORM_OnChar accepted {accepted}/{expectedText.Length}; focused text={focusedText}"));
            int killedText = Native.FORM_ForceToKillFocus(host.Handle);
            events.Add(new { Operation = "commit-text-focus", NativeResult = killedText });

            events.Add(host.FocusAndClick("checkbox", 70, 260));
            int killedCheckbox = Native.FORM_ForceToKillFocus(host.Handle);
            events.Add(new { Operation = "commit-checkbox-focus", NativeResult = killedCheckbox });

            events.Add(host.FocusAndClick("combo", 100, 212));
            int firstDown = Native.FORM_OnKeyDown(host.Handle, host.Page, 0x28, 0);
            string firstDownText = host.ReadFocusedText();
            int enter = Native.FORM_OnKeyDown(host.Handle, host.Page, 0x0D, 0);
            string valueAfterKeyDownOnly = host.ReadFields()[2].Value;
            // Return key-down alone changes the edit preview but does not commit the choice value.
            // A UI host must also deliver the translated return character before saving/blurring.
            int enterCharacter = Native.FORM_OnChar(host.Handle, host.Page, 0x0D, 0);
            events.Add(new { Operation = "combo-keyboard-down-enter", FirstDownResult = firstDown,
                FirstDownText = firstDownText, EnterResult = enter, ValueAfterKeyDownOnly = valueAfterKeyDownOnly,
                EnterCharacterResult = enterCharacter });
            Native.FORM_ForceToKillFocus(host.Handle);

            after = host.ReadFields();
            checks.Add(Check("text-canonical-value-in-memory", after[0].Value == expectedText, $"Text /V = {after[0].Value}"));
            checks.Add(Check("checkbox-mouse-toggle", after[1].Checked && after[1].Value == "Yes", $"Checkbox /V={after[1].Value}, checked={after[1].Checked}"));
            checks.Add(Check("combo-keyboard-selection", after[2].Value == "Beta" && after[2].SelectedIndices.SequenceEqual([1]),
                $"Combo /V={after[2].Value}; selected=[{string.Join(',', after[2].SelectedIndices)}]"));
            host.Save(outputPath);
            callbacks = host.Statistics;
        }

        FieldState[] reopened;
        using (var host = new FormHost(File.ReadAllBytes(outputPath)))
        {
            reopened = host.ReadFields();
            checks.Add(Check("text-save-roundtrip", reopened[0].Value == expectedText, reopened[0].Value));
            checks.Add(Check("checkbox-save-roundtrip", reopened[1].Checked && reopened[1].Value == "Yes", reopened[1].Value));
            checks.Add(Check("combo-save-roundtrip", reopened[2].Value == "Beta" && reopened[2].SelectedIndices.SequenceEqual([1]), reopened[2].Value));
            checks.Add(Check("widgets-retained", reopened.Length == 3 && reopened.Select(x => x.Type).SequenceEqual([6, 2, 4]), "Text, checkbox and combobox widget types survive; no flattening."));
            checks.Add(Check("normal-appearances-retained", reopened.All(x => x.HasAppearance), "Each widget retains a non-empty normal /AP stream after reopening."));
        }

        bool passed = checks.All(x => x.Passed);
        return new
        {
            Id = "POC-03",
            Status = passed ? "limited-feasible" : "failed",
            Description = "Native AcroForm focus, mouse, character and keyboard events, commit-on-blur, full save and reopen.",
            NativeVersion = "bblanchon.PDFium 155.0.8044",
            Inputs = new[] { new { Path = inputPath, Sha256 = Hash(inputPath), Ownership = "Generated by FormsProbe; no third-party document data." } },
            Outputs = new[] { new { Path = outputPath, Sha256 = Hash(outputPath) } },
            Before = before,
            AfterCommit = after,
            AfterReopen = reopened,
            Checks = checks,
            Events = events,
            Callbacks = callbacks,
            Limitations = new[]
            {
                "This probe hosts PDFium directly on one synchronous thread; it does not implement Avalonia focus, tab routing, clipboard, undo, accessibility or IME composition.",
                "Only plain ASCII text, one checkbox and a non-editable combobox are covered. CJK form font embedding, radio groups, multi-select lists, validation, calculation, JavaScript and XFA are not verified.",
                "The host supplies timer identifiers but does not run a UI timer loop; caret blinking and long-lived interaction require a real dispatcher-backed timer implementation.",
                "Windows x64 native execution is the current result. Linux x64/ARM64 and physical keyboard/IME acceptance remain required.",
                "Independent pypdf canonical /AcroForm /Fields /V /AP checks and Poppler rendering are performed by the surrounding verification workflow."
            }
        };
    }

    private sealed record ProbeCheck(string Id, bool Passed, string Detail);
    private static ProbeCheck Check(string id, bool passed, string detail) => new(id, passed, detail);
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private sealed record FieldState(string Name, int Type, string Value, bool Checked, int[] SelectedIndices, bool HasAppearance, ulong AppearanceUtf16Bytes);
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
                        Native.FPDFAnnot_IsChecked(Handle, annot) != 0, selected.ToArray(), appearanceBytes > 2, appearanceBytes));
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
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 400] /Resources << /Font << /Helv 4 0 R >> >> /Contents 6 0 R /Annots [7 0 R 8 0 R 9 0 R] >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Fields [7 0 R 8 0 R 9 0 R] /DR << /Font << /Helv 4 0 R >> >> /DA (/Helv 12 Tf 0 g) /NeedAppearances false >>",
            Stream("BT /Helv 16 Tf 40 365 Td (P0 interactive AcroForm) Tj ET\nBT /Helv 10 Tf 60 335 Td (Text - native character input) Tj ET\nBT /Helv 10 Tf 90 256 Td (Checkbox - mouse click) Tj ET\nBT /Helv 10 Tf 60 235 Td (Combobox - Down then Enter) Tj ET\nBT /Helv 9 Tf 60 170 Td (Fields remain editable after save.) Tj ET"),
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /V () /Rect [60 300 340 325] /F 4 /P 3 0 R /DA (/Helv 12 Tf 0 g) /MK << /BC [0 0 0] /BG [1 1 1] >> /BS << /W 1 /S /S >> /AP << /N 10 0 R >> >>",
            "<< /Type /Annot /Subtype /Widget /FT /Btn /T (Agree) /V /Off /AS /Off /Rect [60 250 80 270] /F 4 /P 3 0 R /AP << /N << /Off 11 0 R /Yes 12 0 R >> >> >>",
            "<< /Type /Annot /Subtype /Widget /FT /Ch /Ff 131072 /T (Choice) /V (Alpha) /I [0] /Opt [(Alpha) (Beta) (Gamma)] /Rect [60 200 220 225] /F 4 /P 3 0 R /DA (/Helv 12 Tf 0 g) /MK << /BC [0 0 0] /BG [1 1 1] >> /BS << /W 1 /S /S >> /AP << /N 13 0 R >> >>",
            Appearance(280, 25, "q 1 1 1 rg 0 0 280 25 re f 0 G 1 w 0.5 0.5 279 24 re S Q"),
            Appearance(20, 20, "q 1 1 1 rg 0 0 20 20 re f 0 G 1 w 0.5 0.5 19 19 re S Q"),
            Appearance(20, 20, "q 1 1 1 rg 0 0 20 20 re f 0 G 1 w 0.5 0.5 19 19 re S 2 w 4 10 m 8 5 l 16 16 l S Q"),
            Appearance(160, 25, "q 1 1 1 rg 0 0 160 25 re f 0 G 1 w 0.5 0.5 159 24 re S BT /Helv 12 Tf 4 8 Td (Alpha) Tj ET Q")
        };
        using var stream = new MemoryStream();
        static void Write(MemoryStream destination, string value) => destination.Write(Encoding.Latin1.GetBytes(value));
        Write(stream, "%PDF-1.7\n%\xe2\xe3\xcf\xd3\n");
        var offsets = new List<long> { 0 };
        for (int index = 0; index < objects.Count; index++)
        {
            offsets.Add(stream.Position);
            Write(stream, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        long startXref = stream.Position;
        Write(stream, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (long offset in offsets.Skip(1)) Write(stream, offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Write(stream, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{startXref}\n%%EOF\n");
        return stream.ToArray();
    }

    private static string Stream(string content) => $"<< /Length {Encoding.Latin1.GetByteCount(content)} >>\nstream\n{content}\nendstream";
    private static string Appearance(int width, int height, string content) =>
        $"<< /Type /XObject /Subtype /Form /BBox [0 0 {width} {height}] /Resources << /Font << /Helv 4 0 R >> >> /Length {Encoding.Latin1.GetByteCount(content)} >>\nstream\n{content}\nendstream";

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
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_GetFormFieldType(IntPtr form, IntPtr annotation);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_GetOptionCount(IntPtr form, IntPtr annotation);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_IsOptionSelected(IntPtr form, IntPtr annotation, int index);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDFAnnot_IsChecked(IntPtr form, IntPtr annotation);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern CULong FPDFAnnot_GetAP(IntPtr annotation, int mode, IntPtr buffer, CULong length);
        [DllImport(Library, CallingConvention = CallingConvention.Winapi)] internal static extern int FPDF_SaveAsCopy(IntPtr document, ref FileWrite writer, CULong flags);
    }
}
