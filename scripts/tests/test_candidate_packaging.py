"""Packaging safety checks; only generated files in TemporaryDirectory are used."""
import hashlib
import importlib.util
import json
from pathlib import Path
import stat
import tempfile
import unittest
from unittest.mock import patch
import zipfile

spec = importlib.util.spec_from_file_location('candidate', Path(__file__).resolve().parents[1] / 'prepare_internal_candidate.py')
candidate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(candidate)


class CandidatePackagingTests(unittest.TestCase):
    def setUp(self):
        self.test_parent = candidate.ROOT / '.tools/PackagingTests'
        self.test_parent.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix='LemonVN.PackagingTest.', dir=self.test_parent)
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.assertTrue(self.root.resolve().is_relative_to(self.test_parent.resolve()))
        self.temp.cleanup()

    def make_zip(self, *, extra=None, wrong_hash=False):
        payloads = {name: b'Synthetic packaging fixture, not a runtime or user image.' for name in candidate.REQUIRED}
        manifest = {'files': [{'path': name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest().upper()}
                              for name, data in payloads.items()]}
        if wrong_hash:
            manifest['files'][0]['sha256'] = '0' * 64
        payloads['FILE_MANIFEST.json'] = json.dumps(manifest).encode()
        archive = self.root / 'fixture.zip'
        with zipfile.ZipFile(archive, 'x') as z:
            for name, data in payloads.items():
                z.writestr('LemonVN-test/' + name, data)
            if extra:
                info, data = extra
                z.writestr(info, data)
        return archive

    def test_valid_archive_checks_all_file_hashes(self):
        result = candidate.verify_zip(self.make_zip())
        self.assertTrue(result['allManifestHashesMatch'])
        self.assertTrue(result['zipCRCValid'])

    def test_tampered_payload_hash_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'hash/length'):
            candidate.verify_zip(self.make_zip(wrong_hash=True))

    def test_unlisted_file_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'file set'):
            candidate.verify_zip(self.make_zip(extra=('LemonVN-test/extra.txt', b'extra')))

    def test_windows_case_collision_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            candidate.verify_zip(self.make_zip(extra=('LemonVN-test/lemonvn.EXE', b'other')))

    def test_zip_symlink_is_rejected(self):
        info = zipfile.ZipInfo('LemonVN-test/linked.txt')
        info.create_system = 3
        info.external_attr = (stat.S_IFLNK | 0o777) << 16
        with self.assertRaisesRegex(ValueError, 'Symlink'):
            candidate.verify_zip(self.make_zip(extra=(info, b'../../elsewhere')))

    def test_unsafe_zip_paths_are_rejected(self):
        for name in ['../outside.txt', '/absolute.txt', 'C:/outside.txt', 'LemonVN-test/../outside.txt',
                     'LemonVN-test\\file.txt', 'LemonVN-test/CON.txt', 'LemonVN-test/name.',
                     'LemonVN-test/./file.txt', 'LemonVN-test//file.txt']:
            with self.subTest(name=name), self.assertRaises(ValueError):
                candidate.safe_name(name)

    def test_private_material_rejected_even_if_in_inactive_asset_directory(self):
        for name in ['sign.pfx', 'Assets/old/key.pem', 'sub/secret.key']:
            with self.subTest(name=name), self.assertRaisesRegex(ValueError, 'Private/signing'):
                candidate.exclusion(Path(name))

    def test_personal_database_and_logs_rejected_before_packaging(self):
        for name in ['lemonvn.db', 'nested/user.sqlite', 'Logs/session.log', 'Data/lemonvn.db-log', 'Backups/export.zip']:
            with self.subTest(name=name), self.assertRaises(ValueError):
                candidate.exclusion(Path(name))

    def test_audited_legacy_assets_excluded_and_own_runtime_cover_kept(self):
        self.assertIsNotNone(candidate.exclusion(Path('Assets/Data/vn_mapper.db')))
        self.assertIsNotNone(candidate.exclusion(Path('Assets/Fonts/Segoe Fluent Icons.ttf')))
        self.assertIsNone(candidate.exclusion(Path('Assets/LemonPlaceholder.png')))
        self.assertIsNone(candidate.exclusion(Path('AutoMapper.dll')))

    def test_runtime_requirements_follow_the_resolved_stable_sdk(self):
        folder = self.root / 'microsoft.windowsappsdk.runtime/2.4.0'
        folder.mkdir(parents=True)
        (folder / 'WindowsAppSDK-VersionInfo.json').write_text(json.dumps({
            'Release': {'Channel': 'stable', 'MajorMinor': {'HexUInt32': '0x00020004'}},
            'Runtime': {'Packages': {'Framework': {'PackageFamilyName': 'SyntheticFramework'}},
                        'Version': {'String': '2.4.0.0'}}}))
        inventory = {'Packages': [{'Id': 'Microsoft.WindowsAppSDK', 'Version': '2.4.0'},
                                  {'Id': 'Microsoft.WindowsAppSDK.Runtime', 'Version': '2.4.0'}]}
        actual = candidate.runtime_requirements(inventory, self.root)
        self.assertEqual('2.4.0', actual['windowsAppSdkNuGet'])
        self.assertEqual('2.4.0.0', actual['minimumFrameworkVersion'])
        self.assertEqual('SyntheticFramework', actual['frameworkPackageFamily'])

    def test_mismatched_runtime_version_cannot_produce_stale_installation_advice(self):
        inventory = {'Packages': [{'Id': 'Microsoft.WindowsAppSDK', 'Version': '2.4.0'},
                                  {'Id': 'Microsoft.WindowsAppSDK.Runtime', 'Version': '2.1.3'}]}
        with self.assertRaisesRegex(ValueError, 'inconsistent'):
            candidate.runtime_requirements(inventory, self.root)

    def test_existing_output_is_not_overwritten(self):
        output = self.root / 'existing'
        output.mkdir()
        marker = output / 'keep.txt'
        marker.write_text('keep')
        with self.assertRaises(FileExistsError):
            candidate.prepare(self.root, output, {}, self.root)
        self.assertEqual('keep', marker.read_text())

    def test_incomplete_release_fails_before_creating_destination(self):
        source, output = self.root / 'source', self.root / 'output'
        source.mkdir()
        (source / 'LemonVN.exe').write_bytes(b'fixture')
        with self.assertRaisesRegex(ValueError, 'incomplete'):
            candidate.prepare(source, output, {}, self.root)
        self.assertFalse(output.exists())

    def test_private_zip_entry_cannot_be_accepted(self):
        with self.assertRaisesRegex(ValueError, 'Forbidden'):
            candidate.verify_zip(self.make_zip(extra=('LemonVN-test/secret.pfx', b'never a real key')))

    def test_local_consent_record_is_rejected_from_build_and_archive(self):
        with self.assertRaisesRegex(ValueError, 'Personal consent'):
            candidate.exclusion(Path('Settings/license-consent.json'))
        with self.assertRaisesRegex(ValueError, 'Forbidden'):
            candidate.verify_zip(self.make_zip(extra=('LemonVN-test/license-consent.json', b'{}')))

    def test_current_verified_bundle_preserves_supplements_and_pending_decisions(self):
        destination = self.root / 'notices'
        result = candidate.stage_verified_notices(candidate.ROOT, destination)
        self.assertEqual(282, result['verifiedNoticeFiles'])
        self.assertEqual([], result['pendingReleaseDecisions'])
        self.assertTrue(result['allBinaryLicenseQuestionsResolved'])
        rtf = 'supplemental/Microsoft.Windows.SDK.NET.Ref/10.0.22621.57/sdk_license.rtf'
        self.assertEqual((candidate.ROOT / 'docs/licenses' / rtf).read_bytes(), (destination / rtf).read_bytes())
        self.assertFalse((destination / 'standard/LGPL-3.0.txt').exists())
        with self.assertRaises(FileExistsError):
            candidate.stage_verified_notices(candidate.ROOT, destination)

    def test_current_winui_material_is_corrected_and_old_preview_is_not_staged(self):
        destination = self.root / 'notices'
        candidate.stage_verified_notices(candidate.ROOT, destination)
        current = destination / 'components/Microsoft.WindowsAppSDK.WinUI/2.3.6/license.txt'
        self.assertEqual('5B11E6347756E40FE0274BC08C97F89201B94F0D50181A09A00F1F4740840501', candidate.sha(current))
        self.assertNotIn('ENGINEERING PREVIEW', current.read_text(encoding='utf-8-sig'))
        self.assertFalse((destination / 'components/Microsoft.WindowsAppSDK.WinUI/2.1.0/license.txt').exists())
        self.assertTrue((destination / 'supplemental/Microsoft.Windows.CsWinRT/2.2.0/LICENSE').is_file())

    def test_uncommitted_build_source_is_rejected(self):
        with patch.object(candidate.subprocess, 'run') as command:
            command.return_value.stdout = ' M synthetic-file.cs\n'
            with self.assertRaisesRegex(ValueError, 'must be clean'):
                candidate.source_commit(self.root)
            self.assertEqual(1, command.call_count)

    def test_stale_release_inventory_is_rejected(self):
        payload = self.root / 'LemonVN.dll'
        payload.write_bytes(b'New synthetic assembly bytes')
        inventory = {'Files': [{'Path': payload.name, 'Bytes': payload.stat().st_size, 'SHA256': '0'*64}]}
        with self.assertRaisesRegex(ValueError, 'does not match'):
            candidate.check_inventory(self.root, [payload], inventory)

    def test_removed_runtime_dependency_cannot_return(self):
        payload = self.root / 'SevenZipSharp.dll'
        payload.write_bytes(b'Synthetic dependency, no real assembly')
        inventory = {'Files': [{'Path': payload.name, 'Bytes': payload.stat().st_size, 'SHA256': candidate.sha(payload)}]}
        with self.assertRaisesRegex(ValueError, 'Removed dependency'):
            candidate.check_inventory(self.root, [payload], inventory)


if __name__ == '__main__':
    unittest.main()
