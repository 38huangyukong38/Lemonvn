"""Audit the owned native launcher; no third-party PE parser or runtime required."""
import argparse
import hashlib
import json
from pathlib import Path
import struct


def inspect_launcher(path):
    if path.stat().st_size > 4 * 1024 * 1024:
        raise ValueError('Not a bounded native launcher')
    data = path.read_bytes()
    def u16(at): return struct.unpack_from('<H', data, at)[0]
    def u32(at): return struct.unpack_from('<I', data, at)[0]
    try:
        if not 512 <= len(data) <= 4 * 1024 * 1024 or data[:2] != b'MZ':
            raise ValueError('Not a bounded native launcher')
        pe = u32(0x3c)
        if data[pe:pe + 4] != b'PE\0\0' or u16(pe + 4) != 0x8664:
            raise ValueError('Launcher must be Windows x64 PE')
        count, size = u16(pe + 6), u16(pe + 20)
        opt = pe + 24
        if not 1 <= count <= 32 or size < 240 or u16(opt) != 0x20b or u16(opt + 68) != 2:
            raise ValueError('Launcher must be native Windows GUI PE32+')
        if not u32(opt + 16) or u32(opt + 112 + 14 * 8) or u32(opt + 112 + 13 * 8):
            raise ValueError('Launcher cannot use CLR or delayed dependencies')
        if u16(opt + 70) & 0x160 != 0x160:
            raise ValueError('Launcher requires ASLR, high entropy and DEP')
        sections = []
        for n in range(count):
            at = opt + size + n * 40
            sections.append((u32(at + 12), max(u32(at + 8), u32(at + 16)), u32(at + 20)))
        def offset(rva):
            for virtual, length, raw in sections:
                if virtual <= rva < virtual + length and raw + rva - virtual < len(data):
                    return raw + rva - virtual
            raise ValueError('Invalid launcher PE reference')
        imports = []
        at = offset(u32(opt + 112 + 8))
        for _ in range(16):
            row = struct.unpack_from('<IIIII', data, at)
            if not any(row): break
            name = offset(row[3]);end = data.index(b'\0', name, name + 128)
            imports.append(data[name:end].decode('ascii').upper());at += 20
        else: raise ValueError('Too many native imports')
        if set(imports) != {'KERNEL32.DLL', 'USER32.DLL'}:
            raise ValueError('Unexpected runtime dependency in launcher')
        at = offset(u32(opt + 112 + 2 * 8))
        types = {u32(at + 16 + n * 8) for n in range(u16(at + 12), u16(at + 12) + u16(at + 14))}
        if not {3, 16, 24} <= types or b'level="asInvoker"' not in data:
            raise ValueError('Launcher must retain icon, version and asInvoker manifest')
        return {'architecture': 'x64', 'nativeWindowsGui': True, 'imports': sorted(set(imports)),
                'clrRuntimeRequired': False, 'asInvoker': True, 'iconAndVersionPresent': True,
                'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest().upper()}
    except (IndexError, struct.error, UnicodeError) as error:
        raise ValueError('Malformed native launcher') from error


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('file', type=Path)
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    result = inspect_launcher(args.file)
    if args.report: args.report.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result, indent=2))
