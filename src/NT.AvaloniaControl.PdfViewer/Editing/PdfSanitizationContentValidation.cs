using PdfSharp.Pdf.Content.Objects;

namespace NT.AvaloniaControl.PdfViewer.Editing;

/// <summary>
/// Rejects extra operands and wrong primitive types instead of serializing hidden
/// strings or dictionaries that a tolerant content interpreter might ignore.
/// Resource existence and content structure are checked by the sanitizer itself.
/// </summary>
internal static class PdfSanitizationContentValidation
{
    internal static void Validate(COperator operation)
    {
        switch (operation.Name)
        {
            case "q": case "Q": case "BT": case "ET": case "h": case "S": case "s":
            case "f": case "F": case "f*": case "B": case "B*": case "b": case "b*":
            case "n": case "W": case "W*": case "T*": case "BX": case "EX": case "EMC":
                Count(0); break;
            case "cm": case "Tm": case "c":
                Numbers(6); break;
            case "re": case "v": case "y":
                Numbers(4); break;
            case "m": case "l": case "Td": case "TD":
                Numbers(2); break;
            case "Tc": case "Tw": case "Tz": case "TL": case "Ts":
                Numbers(1); break;
            case "w":
                Numbers(1); Nonnegative(0); break;
            case "M":
                Numbers(1); if (Number(0) < 1) Fail(); break;
            case "i":
                Numbers(1); if (Number(0) is < 0 or > 100) Fail(); break;
            case "J": case "j":
                IntegerRange(0, 2); break;
            case "Tr":
                IntegerRange(0, 7); break;
            case "d":
                Count(2);
                if (operation.Operands[0] is not CArray dashes) { Fail(); break; }
                if (dashes.Count > 4096) Fail();
                var hasPositive = false;
                foreach (var dash in dashes)
                {
                    var length = Numeric(dash);
                    if (length < 0) Fail();
                    hasPositive |= length > 0;
                }
                if (dashes.Count != 0 && !hasPositive) Fail();
                Nonnegative(1);
                break;
            case "ri":
                Count(1);
                if (Name(0) is not ("AbsoluteColorimetric" or "RelativeColorimetric" or "Saturation" or "Perceptual")) Fail();
                break;
            case "Tf":
                Count(2); _ = Name(0); Nonnegative(1); break;
            case "Do": case "gs": case "CS": case "cs": case "BMC": case "MP":
                Count(1); _ = Name(0); break;
            case "Tj": case "'":
                Count(1); String(0); break;
            case "\"":
                Count(3); _ = Number(0); _ = Number(1); String(2); break;
            case "TJ":
                Count(1);
                if (operation.Operands[0] is not CArray adjustments) { Fail(); break; }
                if (adjustments.Count > 1_000_000) Fail();
                foreach (var item in adjustments)
                    if (item is not CString) _ = Numeric(item);
                break;
            case "G": case "g":
                Colors(1); break;
            case "RG": case "rg":
                Colors(3); break;
            case "K": case "k":
                Colors(4); break;
            case "SC": case "sc": case "SCN": case "scn":
                // The resource layer admits only Device and Indexed spaces. Patterns,
                // component dictionaries, names and arbitrary extra operands are refused.
                if (operation.Operands.Count is < 1 or > 4) Fail();
                Numbers(operation.Operands.Count);
                break;
            case "BDC": case "DP":
                throw new NotSupportedException("Marked-content properties require an unsupported sanitizer implementation.");
            default:
                throw new NotSupportedException($"Unsupported painting operator '{operation.Name}'.");
        }

        void Count(int expected)
        {
            if (operation.Operands.Count != expected) Fail();
        }

        void Numbers(int expected)
        {
            Count(expected);
            for (var index = 0; index < expected; ++index) _ = Number(index);
        }

        double Number(int index) => Numeric(operation.Operands[index]);

        void Nonnegative(int index)
        {
            if (Number(index) < 0) Fail();
        }

        void IntegerRange(int minimum, int maximum)
        {
            Count(1);
            if (operation.Operands[0] is not CInteger number || number.Value < minimum || number.Value > maximum) Fail();
        }

        void Colors(int count)
        {
            Numbers(count);
            for (var index = 0; index < count; ++index)
                if (Number(index) is < 0 or > 1) Fail();
        }

        string Name(int index)
        {
            if (operation.Operands[index] is not CName name) { Fail(); return ""; }
            var result = name.Name.StartsWith('/') ? name.Name[1..] : name.Name;
            if (result.Length == 0) Fail();
            return result;
        }

        void String(int index)
        {
            if (operation.Operands[index] is not CString) Fail();
        }

        void Fail() => throw new NotSupportedException($"Malformed or unsupported operands for PDF painting operator '{operation.Name}'.");
    }

    private static double Numeric(CObject value)
    {
        var number = value switch { CInteger integer => integer.Value, CReal real => real.Value, _ => double.NaN };
        if (!double.IsFinite(number))
            throw new NotSupportedException("A PDF painting operator has a non-numeric, non-finite or nested operand.");
        return number;
    }
}
