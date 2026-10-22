"""Small synthetic PE/archive fixtures only; never execute them or touch user data."""
import hashlib
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import native_launcher
import prepare_internal_candidate as candidate
import prepare_preview_candidate as preview


def synthetic_pe():
    data = bytearray(0xa00)
    def w16(at, value): struct.pack_into('<H', data, at, value)
    def w32(at, value): struct.pack_into('<I', data, at, value)
    data[:2] = b'MZ';w32(0x3c, 0x80);data[0x80:0x84] = b'PE\0\0'
    w16(0x84, 0x8664);w16(0x86, 1);w16(0x94, 240)
    opt = 0x98;w16(opt, 0x20b);w32(opt + 16, 0x1000);w16(opt + 68, 2);w16(opt + 70, 0x160)
    w32(opt + 120, 0x1100);w32(opt + 128, 0x1200)
    section = opt + 240;w32(section + 8, 0x800);w32(section + 12, 0x1000);w32(section + 16, 0x800);w32(section + 20, 0x200)
    w32(0x30c, 0x1150);w32(0x320, 0x1160)
    data[0x350:0x35d] = b'KERNEL32.dll\0';data[0x360:0x36b] = b'USER32.dll\0'
    w16(0x40e, 3)
    for n, value in enumerate([3, 16, 24]): w32(0x410 + n * 8, value)
    value = b'level="asInvoker"';data[0x500:0x500 + len(value)] = value
    return data


class OrganizedCandidateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='LemonVN-organized-fixture-')
        self.root = Path(self.temp.name)
    def tearDown(self): self.temp.cleanup()
    def fixture(self, *, omit=None, layout='app-subdirectory-v1'):
        files = {'app/' + n: b'Synthetic runtime fixture' for n in candidate.REQUIRED - {'Uninstall.exe'}}
        files.update({n: b'Synthetic owned root file' for n in ['LemonVN.exe', 'Uninstall.exe', 'README.md', 'LICENSE', 'NOTICE']})
        if omit: files.pop(omit)
        manifest = {'layout': layout, 'files': [{'path': n, 'bytes': len(v), 'sha256': hashlib.sha256(v).hexdigest().upper()} for n, v in files.items()]}
        files['FILE_MANIFEST.json'] = json.dumps(manifest).encode()
        path = self.root / 'fixture.zip'
        with zipfile.ZipFile(path, 'x') as z:
            for n, value in files.items(): z.writestr('LemonVN-test/' + n, value)
        return path
    def test_organized_runtime_and_root_entries_are_all_hashed(self):
        self.assertTrue(candidate.verify_zip(self.fixture())['allManifestHashesMatch'])
    def test_missing_nested_runtime_is_not_hidden_by_root_launcher(self):
        with self.assertRaisesRegex(ValueError, 'required'): candidate.verify_zip(self.fixture(omit='app/LemonVN.dll'))
    def test_unknown_layout_cannot_weaken_payload_validation(self):
        with self.assertRaisesRegex(ValueError, 'Unsupported'): candidate.verify_zip(self.fixture(layout='other'))
    def test_revision_cannot_inject_paths(self):
        for value in ['../r2', 'r2/other', '', 'v0.2.0', 'r0']:
            with self.subTest(value=value), self.assertRaisesRegex(ValueError, 'numeric'):
                preview.prepare(Path('.'), Path('unused'), {}, Path('.'), revision=value)
    def test_native_launcher_has_no_extra_runtime(self):
        p = self.root / 'synthetic.exe';p.write_bytes(synthetic_pe())
        report = native_launcher.inspect_launcher(p)
        self.assertEqual(['KERNEL32.DLL', 'USER32.DLL'], report['imports'])
        self.assertFalse(report['clrRuntimeRequired'])
    def test_extra_native_runtime_import_is_rejected(self):
        data = synthetic_pe();data[0x350:0x35d] = b'VCRUN140.dll\0'
        p = self.root / 'synthetic.exe';p.write_bytes(data)
        with self.assertRaisesRegex(ValueError, 'dependency'): native_launcher.inspect_launcher(p)
    def test_clr_or_wrong_architecture_is_rejected(self):
        for at, value, fmt in [(0x84, 0x14c, '<H'), (0x98 + 112 + 14 * 8, 0x1234, '<I')]:
            with self.subTest(at=at):
                data = synthetic_pe();struct.pack_into(fmt, data, at, value)
                p = self.root / 'synthetic.exe';p.write_bytes(data)
                with self.assertRaises(ValueError): native_launcher.inspect_launcher(p)
    def test_missing_asinvoker_is_rejected(self):
        data = synthetic_pe();data[0x500:0x510] = b'level="anything"'
        p = self.root / 'synthetic.exe';p.write_bytes(data)
        with self.assertRaisesRegex(ValueError, 'asInvoker'): native_launcher.inspect_launcher(p)


if __name__ == '__main__': unittest.main()
