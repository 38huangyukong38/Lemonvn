import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('license_materials', Path(__file__).resolve().parents[1]/'verify_license_materials.py')
materials = importlib.util.module_from_spec(spec)
spec.loader.exec_module(materials)


class NoticeFileSafetyTests(unittest.TestCase):
    def test_original_notice_bytes_are_required(self):
        with tempfile.TemporaryDirectory(prefix='LemonVN-NoticeTest-') as temp:
            root = Path(temp)
            source = root/'LICENSE.txt'
            source.write_bytes(b'Original copyright\r\nOriginal license.\r\n')
            digest = hashlib.sha256(source.read_bytes()).hexdigest()
            self.assertEqual(materials.checked_file(root, 'LICENSE.txt', digest), source)
            source.write_bytes(b'Original copyright\nOriginal license.\n')
            with self.assertRaises(ValueError):
                materials.checked_file(root, 'LICENSE.txt', digest)

    def test_outside_and_windows_absolute_paths_are_rejected(self):
        with tempfile.TemporaryDirectory(prefix='LemonVN-NoticeTest-') as temp:
            for name in ['../LICENSE', '/LICENSE', 'Q:/synthetic-test/not-a-notice', 'folder\\LICENSE']:
                with self.subTest(name=name), self.assertRaises(ValueError):
                    materials.checked_file(Path(temp), name, '0'*64)

    def test_binary_material_cannot_be_staged_as_a_notice(self):
        with tempfile.TemporaryDirectory(prefix='LemonVN-NoticeTest-') as temp:
            source = Path(temp)/'private.pfx'
            source.write_bytes(b'Synthetic test bytes, no certificate.')
            with self.assertRaises(ValueError):
                materials.checked_file(Path(temp), source.name, hashlib.sha256(source.read_bytes()).hexdigest())

    def test_missing_notice_is_rejected(self):
        with tempfile.TemporaryDirectory(prefix='LemonVN-NoticeTest-') as temp:
            with self.assertRaises(ValueError):
                materials.checked_file(Path(temp), 'LICENSE', '0'*64)


if __name__ == '__main__':
    unittest.main()
