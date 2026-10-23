"""Bounded, independent PNG/pixel checks for native chart CI evidence.

Only the standard library is used. The supported input contract is noninterlaced
8-bit RGB or RGBA PNG; features that change pixel meaning but are not implemented
(for example tRNS) are rejected. A PNG is decoded into tightly packed, straight
RGBA. The native producer's premultiplied BGRA hash is computed separately.
"""

from __future__ import annotations

import hashlib
import math
from pathlib import Path
import struct
import zlib


MAX_PIXELS = 16 * 1024 * 1024
MAX_DIMENSION = 16_384
MAX_FILE_BYTES = 128 * 1024 * 1024
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"


class PngError(ValueError):
    """Invalid, unsupported, oversized or ambiguous PNG evidence."""


def _dimensions(width: int, height: int) -> None:
    if not 0 < width <= MAX_DIMENSION or not 0 < height <= MAX_DIMENSION or width * height > MAX_PIXELS:
        raise PngError("PNG dimensions exceed the native evidence limit")


def _byte_add(a: int, b: int, even_mask: int, full_mask: int) -> int:
    # Each 16-bit lane contains one byte plus eight zero bits, so addition never
    # carries into the next lane. Combining both parities gives bytewise mod256.
    even = ((a & even_mask) + (b & even_mask)) & even_mask
    odd = ((((a >> 8) & even_mask) + ((b >> 8) & even_mask)) & even_mask) << 8
    return (even | odd) & full_mask


def decode_png(path: str | Path) -> tuple[int, int, bytes]:
    """Decode strictly bounded RGB8/RGBA8 PNG and verify all chunk CRCs.

    Trailing file data, a second/extra zlib stream, noncontiguous IDAT, duplicate
    structural chunks, decompression expansion and truncated trailers all fail.
    """
    file_path = Path(path)
    if file_path.stat().st_size > MAX_FILE_BYTES:
        raise PngError("PNG file exceeds the evidence byte limit")
    data = file_path.read_bytes()
    if len(data) > MAX_FILE_BYTES or not data.startswith(PNG_SIGNATURE):
        raise PngError("Invalid PNG signature or size")
    position = len(PNG_SIGNATURE)
    header: tuple[int, ...] | None = None
    compressed: list[bytes] = []
    idat_bytes = 0
    idat_closed = False
    saw_palette = False
    ended = False
    while position < len(data):
        if len(data) - position < 12:
            raise PngError("Truncated PNG chunk header/trailer")
        length = struct.unpack_from(">I", data, position)[0]
        kind = data[position + 4 : position + 8]
        if length > MAX_FILE_BYTES or length > len(data) - position - 12:
            raise PngError("Truncated or oversized PNG chunk")
        if not all(65 <= byte <= 90 or 97 <= byte <= 122 for byte in kind) or kind[2] & 32:
            raise PngError("Invalid PNG chunk type")
        chunk = data[position + 8 : position + 8 + length]
        expected_crc = struct.unpack_from(">I", data, position + 8 + length)[0]
        actual_crc = zlib.crc32(chunk, zlib.crc32(kind)) & 0xFFFFFFFF
        if expected_crc != actual_crc:
            raise PngError("PNG chunk CRC mismatch")
        position += length + 12
        if header is None and kind != b"IHDR":
            raise PngError("IHDR must be the first chunk")
        if kind == b"IHDR":
            if header is not None or length != 13:
                raise PngError("Duplicate or malformed IHDR")
            header = struct.unpack(">IIBBBBB", chunk)
            width, height, depth, color, compression, filtering, interlace = header
            _dimensions(width, height)
            if depth != 8 or color not in (2, 6) or compression != 0 or filtering != 0 or interlace != 0:
                raise PngError("Only noninterlaced RGB8/RGBA8 PNG is supported")
        elif kind == b"PLTE":
            if saw_palette or compressed or length == 0 or length > 768 or length % 3:
                raise PngError("Duplicate, misplaced or malformed PLTE")
            saw_palette = True
        elif kind == b"IDAT":
            if idat_closed:
                raise PngError("IDAT chunks must be contiguous")
            idat_bytes += length
            if idat_bytes > MAX_FILE_BYTES:
                raise PngError("Compressed PNG exceeds the evidence byte limit")
            compressed.append(chunk)
        elif kind == b"IEND":
            if length != 0 or not compressed:
                raise PngError("Malformed IEND or missing IDAT")
            if position != len(data):
                raise PngError("Trailing bytes or extra chunks after IEND")
            ended = True
            break
        else:
            if kind in (b"tRNS", b"acTL", b"fcTL", b"fdAT"):
                raise PngError("Transparency-key and animated PNG are unsupported")
            if not kind[0] & 32:
                raise PngError("Unknown critical PNG chunk")
            if compressed:
                idat_closed = True
    if header is None or not ended:
        raise PngError("Missing PNG header or IEND")
    width, height, _, color, *_ = header
    bpp = 4 if color == 6 else 3
    row_bytes = width * bpp
    expected_bytes = (row_bytes + 1) * height
    try:
        inflater = zlib.decompressobj()
        decoded = inflater.decompress(b"".join(compressed), expected_bytes + 1)
    except zlib.error as error:
        raise PngError("Invalid PNG zlib stream") from error
    if len(decoded) != expected_bytes or not inflater.eof or inflater.unused_data or inflater.unconsumed_tail:
        raise PngError("PNG stream is truncated, overexpanded or contains extra compressed data")

    rgba = bytearray(width * height * 4)
    previous = bytes(row_bytes)
    full_mask = (1 << (row_bytes * 8)) - 1
    even_mask = int.from_bytes(b"\xff\x00" * ((row_bytes + 1) // 2), "little") & full_mask
    for y in range(height):
        source = y * (row_bytes + 1)
        method = decoded[source]
        row = decoded[source + 1 : source + 1 + row_bytes]
        if method == 1:
            value = int.from_bytes(row, "little")
            shift = bpp * 8
            while shift < row_bytes * 8:
                value = _byte_add(value, (value << shift) & full_mask, even_mask, full_mask)
                shift *= 2
            row = value.to_bytes(row_bytes, "little")
        elif method == 2:
            row = _byte_add(int.from_bytes(row, "little"), int.from_bytes(previous, "little"), even_mask, full_mask).to_bytes(row_bytes, "little")
        elif method in (3, 4):
            work = bytearray(row)
            for x in range(row_bytes):
                left = work[x - bpp] if x >= bpp else 0
                up = previous[x]
                if method == 3:
                    predictor = (left + up) // 2
                else:
                    upper_left = previous[x - bpp] if x >= bpp else 0
                    pa = abs(up - upper_left)
                    pb = abs(left - upper_left)
                    pc = abs(left + up - 2 * upper_left)
                    predictor = left if pa <= pb and pa <= pc else up if pb <= pc else upper_left
                work[x] = (work[x] + predictor) & 255
            row = bytes(work)
        elif method != 0:
            raise PngError("Unknown PNG row filter")
        destination = y * width * 4
        if bpp == 4:
            rgba[destination : destination + width * 4] = row
        else:
            end = destination + width * 4
            rgba[destination:end:4] = row[0::3]
            rgba[destination + 1 : end : 4] = row[1::3]
            rgba[destination + 2 : end : 4] = row[2::3]
            rgba[destination + 3 : end : 4] = b"\xff" * width
        previous = row
    return width, height, bytes(rgba)


def sha_bgra(rgba: bytes) -> str:
    """Hash tightly packed BGRA8888/premultiplied pixels like native CopyPixels."""
    if len(rgba) % 4:
        raise ValueError("RGBA payload must contain complete pixels")
    alpha = rgba[3::4]
    result = bytearray(len(rgba))
    result[3::4] = alpha
    if alpha == b"\xff" * len(alpha):
        result[0::4] = rgba[2::4]
        result[1::4] = rgba[1::4]
        result[2::4] = rgba[0::4]
    else:
        for offset in range(0, len(rgba), 4):
            a = rgba[offset + 3]
            result[offset] = (rgba[offset + 2] * a + 127) // 255
            result[offset + 1] = (rgba[offset + 1] * a + 127) // 255
            result[offset + 2] = (rgba[offset] * a + 127) // 255
    return hashlib.sha256(result).hexdigest().upper()


_RED = bytes(int(value <= 45) for value in range(256))
_GREEN = bytes(int(68 <= value <= 116) for value in range(256))
_BLUE = bytes(int(169 <= value <= 217) for value in range(256))
_ALPHA = bytes(int(value >= 250) for value in range(256))


def blue_counts(width: int, height: int, rgba: bytes, plotrects: list[list[float]]) -> tuple[int, list[int]]:
    """Count #155CC1 ±24, alpha >=250 globally and in strict plot ROIs.

    ROIs are [x,y,width,height]. Pixel loops match the producer's ceil(left/top)
    through floor(right/bottom)-1 rule, clamped to the actual capture dimensions.
    """
    _dimensions(width, height)
    if len(rgba) != width * height * 4:
        raise ValueError("RGBA payload dimensions do not match")
    masks = [rgba[0::4].translate(_RED), rgba[1::4].translate(_GREEN), rgba[2::4].translate(_BLUE), rgba[3::4].translate(_ALPHA)]
    flags = int.from_bytes(masks[0], "little") & int.from_bytes(masks[1], "little") & int.from_bytes(masks[2], "little") & int.from_bytes(masks[3], "little")
    total = flags.bit_count()
    flag_bytes = flags.to_bytes(width * height, "little")
    counts: list[int] = []
    for rectangle in plotrects:
        if len(rectangle) != 4 or any(not isinstance(v, (int, float)) or isinstance(v, bool) or not math.isfinite(v) for v in rectangle):
            raise ValueError("Plot ROI must contain four finite coordinates")
        x, y, w, h = rectangle
        if w <= 0 or h <= 0:
            raise ValueError("Plot ROI must have positive extents")
        left, right = max(0, math.ceil(x)), min(width, math.floor(x + w))
        top, bottom = max(0, math.ceil(y)), min(height, math.floor(y + h))
        counts.append(sum(sum(flag_bytes[row * width + left : row * width + right]) for row in range(top, bottom)) if left < right and top < bottom else 0)
    return total, counts


def _chunk(kind: bytes, data: bytes) -> bytes:
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(data, zlib.crc32(kind)) & 0xFFFFFFFF)


def _write_png(path: str | Path, width: int, height: int, rgba: bytes) -> str:
    _dimensions(width, height)
    if len(rgba) != width * height * 4:
        raise ValueError("RGBA payload dimensions do not match")
    destination = Path(path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    raw = b"".join(b"\x00" + rgba[y * width * 4 : (y + 1) * width * 4] for y in range(height))
    payload = PNG_SIGNATURE + _chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)) + _chunk(b"IDAT", zlib.compress(raw)) + _chunk(b"IEND", b"")
    with destination.open("xb") as stream:
        stream.write(payload)
    return str(destination)


def compare_png(candidate: str | Path, baseline: str | Path, diffOutput: str | Path) -> dict:
    """Decode both files and write a new lossless magenta difference PNG.

    For a dimension mismatch the candidate-sized artifact is solid magenta;
    difference count/max values are failure sentinels, not a pixel alignment.
    Inputs and an existing difference artifact are never modified.
    """
    width, height, actual = decode_png(candidate)
    reference_width, reference_height, expected = decode_png(baseline)
    equal_dimensions = width == reference_width and height == reference_height
    pixels = max(width * height, reference_width * reference_height)
    if not equal_dimensions:
        differences, maximum = pixels, 255
        diff = b"\xff\x00\xff\xff" * (width * height)
    elif actual == expected:
        differences, maximum = 0, 0
        diff = b"\x00\x00\x00\xff" * pixels
    else:
        differences = maximum = 0
        result = bytearray(len(actual))
        result[3::4] = b"\xff" * pixels
        for offset in range(0, len(actual), 4):
            delta = max(abs(actual[offset + channel] - expected[offset + channel]) for channel in range(4))
            if delta:
                differences += 1
                maximum = max(maximum, delta)
                result[offset] = result[offset + 2] = delta
        diff = bytes(result)
    diff_path = _write_png(diffOutput, width, height, diff)
    return {"dimensionsEqual": equal_dimensions, "differentPixels": differences, "pixelCount": pixels, "maxChannelDifference": maximum, "diffPath": diff_path}
