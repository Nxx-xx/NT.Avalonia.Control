"""Create a self-signed, cryptographically real detached CMS PDF fixture.

Synthetic test content; no private signing key is persisted. Not a trust/DocMDP
fixture. Requires cryptography, and uses only the Python standard library for PDF.
"""
import argparse
import datetime as dt
import hashlib
import json
from pathlib import Path
from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.hazmat.primitives.serialization import pkcs7
from cryptography.x509.oid import NameOID


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    existing = [output / "signed-input.pdf", output / "signed-input.json", output / "signer-public.pem"]
    if any(path.exists() for path in existing):
        if not all(path.exists() for path in existing):
            raise RuntimeError("Incomplete frozen signature fixture; use a new run directory.")
        metadata = json.loads(existing[1].read_text(encoding="utf-8"))
        if hashlib.sha256(existing[0].read_bytes()).hexdigest() != metadata["sha256"]:
            raise RuntimeError("Frozen signature fixture hash mismatch.")
        print(f"Reusing frozen signature fixture: {existing[0]}")
        return
    reserve = 16384
    objects = [
        b"<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [6 0 R] /SigFlags 3 >> >>",
        b"<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
        b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 400] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R /Annots [6 0 R] >>",
        b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        b"<< /Length 58 >>\nstream\nBT /F1 16 Tf 30 330 Td (P0 signed original document) Tj ET\nendstream",
        b"<< /Type /Annot /Subtype /Widget /FT /Sig /T (P0Signature) /Rect [0 0 0 0] /P 3 0 R /V 7 0 R >>",
        b"<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange [0000000000 0000000000 0000000000 0000000000] /Contents <" + b"0" * (reserve * 2) + b"> /Reason (Synthetic P0 fixture, no certification policy) >>",
    ]
    pdf = bytearray(b"%PDF-1.7\n%\xe2\xe3\xcf\xd3\n")
    offsets = [0]
    for i, obj in enumerate(objects, 1):
        offsets.append(len(pdf))
        pdf.extend(f"{i} 0 obj\n".encode() + obj + b"\nendobj\n")
    xref = len(pdf)
    pdf.extend(f"xref\n0 {len(objects)+1}\n0000000000 65535 f \n".encode())
    for offset in offsets[1:]:
        pdf.extend(f"{offset:010d} 00000 n \n".encode())
    pdf.extend(f"trailer\n<< /Size {len(objects)+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n".encode())
    begin = pdf.index(b"/Contents <") + len(b"/Contents ")
    end = begin + 2 + reserve * 2
    marker = b"0000000000 0000000000 0000000000 0000000000"
    ranges = f"{0:010d} {begin:010d} {end:010d} {len(pdf)-end:010d}".encode()
    at = pdf.index(marker)
    pdf[at:at+len(marker)] = ranges
    signed_bytes = bytes(pdf[:begin] + pdf[end:])
    key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    name = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, "PDF Viewer P0 Synthetic Signer")])
    now = dt.datetime.now(dt.timezone.utc)
    cert = (x509.CertificateBuilder().subject_name(name).issuer_name(name).public_key(key.public_key())
            .serial_number(x509.random_serial_number()).not_valid_before(now-dt.timedelta(days=1))
            .not_valid_after(now+dt.timedelta(days=30)).sign(key, hashes.SHA256()))
    cms = (pkcs7.PKCS7SignatureBuilder().set_data(signed_bytes).add_signer(cert, key, hashes.SHA256())
           .sign(serialization.Encoding.DER, [pkcs7.PKCS7Options.DetachedSignature, pkcs7.PKCS7Options.Binary]))
    assert len(cms) < reserve
    pdf[begin+1:end-1] = cms.hex().upper().encode().ljust(reserve*2, b"0")
    path = output / "signed-input.pdf"
    path.write_bytes(pdf)
    (output / "signer-public.pem").write_bytes(cert.public_bytes(serialization.Encoding.PEM))
    (output / "signed-input.json").write_text(json.dumps({
        "fixture": "self-signed-CMS-detached", "sha256": hashlib.sha256(pdf).hexdigest(),
        "byte_range": [0, begin, end, len(pdf)-end], "cms_bytes": len(cms),
        "trust": "synthetic self-signed certificate; cryptographic integrity only, not certificate trust or DocMDP"
    }, indent=2), encoding="utf-8")
    print(path)


if __name__ == "__main__":
    main()
