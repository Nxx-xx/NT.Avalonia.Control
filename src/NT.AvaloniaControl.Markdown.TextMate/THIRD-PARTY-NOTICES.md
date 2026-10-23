# Third-party notices

This optional adapter references the following unmodified NuGet packages. It does not
copy their grammar, theme or tokenizer implementation into the NT assembly.

| Package | Version | Package-declared license | Source revision |
| --- | --- | --- | --- |
| TextMateSharp | 2.0.4 | MIT | `622d1b240a5be474939857c0b2780249022f14a3` |
| TextMateSharp.Grammars | 2.0.4 | MIT, with third-party grammar resources | `622d1b240a5be474939857c0b2780249022f14a3` |
| Onigwrap | 1.0.11 | MIT; Oniguruma and other notices retained | `dd8a1cbbe3190c18033b96f2922b95c1761ae793` |

The `Licenses/` directory includes TextMateSharp's MIT license, Onigwrap's own MIT
license and its complete upstream `THIRD-PARTY-NOTICES.TXT` (Oniguruma, Fluent
Solutions/onigwrap, TextMateSharp and SkiaSharp attribution).

The published Grammars assembly's embedded language-package metadata is reproduced
in `Licenses/grammar-metadata.json`. On 2026-10-13, 50 of its 51 language packages
declared MIT and `typst` / `tinymist 0.12.18` declared Apache-2.0. The corresponding
Tinymist v0.12.18 Apache license, including its attribution, is preserved as
`Licenses/Tinymist-Apache-2.0.txt`. The MIT package declaration must not be read as a
claim that each embedded third-party grammar has the same license.

Sources: [TextMateSharp](https://github.com/danipen/TextMateSharp/tree/622d1b240a5be474939857c0b2780249022f14a3),
[Onigwrap](https://github.com/aikawayataro/Onigwrap/tree/dd8a1cbbe3190c18033b96f2922b95c1761ae793),
[Tinymist v0.12.18](https://github.com/Myriad-Dreamin/tinymist/tree/v0.12.18).

Keep these notices and the dependent packages' notices when distributing an application.
No third-party component is relicensed by this adapter.
