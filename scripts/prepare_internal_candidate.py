"""Build and verify a personal/local-only LemonVN candidate from a reviewed Release.

No download, signing, user-data access, history rewriting or publishing. Existing
destinations are never overwritten. Legacy DLLs stay until compile dependencies
are removed; only audited inactive assets, symbols and development files are omitted.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PRIVATE = {'.pfx', '.p12', '.pem', '.key', '.snk', '.cer', '.crt'}
PERSONAL = {'.db', '.sqlite', '.sqlite3', '.log', '.bak', '.zip'}
OWN_ASSETS = {'Assets/LemonPlaceholder.png', 'Assets/LemonAppIcon.ico', 'Assets/LemonAppIcon.png',
              'Assets/LemonStoreLogo.png', 'Assets/LemonSquare44x44Logo.png',
              'Assets/LemonSquare150x150Logo.png', 'Assets/LemonSquare71x71Logo.png'}
REQUIRED = {'LemonVN.exe', 'Uninstall.exe', 'UninstallWorker.ps1', 'LemonVN.dll', 'LemonVN.deps.json', 'LemonVN.runtimeconfig.json',
            'LemonVN.pri', 'GalgameManager.WinApp.Base.pri', 'coreclr.dll', 'LiteDB.dll',
            'Microsoft.Windows.SDK.NET.dll', 'WinRT.Runtime.dll', 'Microsoft.WinUI.dll',
            'LICENSE', 'NOTICE', 'THIRD_PARTY_NOTICES.md', 'SDK_DISTRIBUTION_REVIEW.md',
            'SDK_END_USER_TERMS_DRAFT.md', 'licenses/components/microsoft.windowsappsdk/2.4.0/license.txt',
            'licenses/supplemental/Microsoft.Windows.CsWinRT/2.2.0/LICENSE',
            'licenses/components/Microsoft.Windows.AI.MachineLearning/2.1.74/license.txt', 'licenses/supplemental/Microsoft.Windows.SDK.NET.Ref/10.0.22621.57/sdk_license.rtf', *OWN_ASSETS}
NOTICE_MARKERS = ('license', 'notice', 'copying', 'copyright')


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest().upper()


def exclusion(path):
    name = path.as_posix()
    if path.suffix.lower() in PRIVATE:
        raise ValueError('Private/signing material in build: ' + name)
    if path.name.lower() == 'license-consent.json':
        raise ValueError('Personal consent record must not enter a distribution: ' + name)
    if name.startswith('Assets/') and name not in OWN_ASSETS:
        return 'Inactive or superseded asset outside the current runtime allow-list; source preserved'
    if path.suffix.lower() in PERSONAL or name.lower().endswith(('.db-log', '.db-shm', '.db-wal')):
        raise ValueError('Unexpected user data/log/archive in build: ' + name)
    if path.suffix.lower() == '.pdb':
        return 'Debug symbols; private developer paths are not distributed'
    if name.startswith('Strings/') and path.suffix.lower() == '.resw':
        return 'Source resource XML; localized MRT resources remain in PRI'
    if path.name in {'.gitignore', 'fetch_data.sh'}:
        return 'Development-only file'
    if any(part.lower() in {'sdk', 'testresults', 'tests', 'obj', 'logs', 'backups', 'covers'} for part in path.parts):
        raise ValueError('Unexpected development/personal directory: ' + name)
    return None


def safe_name(name):
    parts = PurePosixPath(name).parts
    if not parts or any(ord(c) < 32 for c in name) or PurePosixPath(name).as_posix() != name or name.startswith(('/', '\\')) or '\\' in name or ':' in name or '..' in parts:
        raise ValueError('Unsafe archive path')
    if any(part.endswith(('.', ' ')) or re.fullmatch(r'(?i)(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\..*)?', part) for part in parts):
        raise ValueError('Unsafe Windows path')
    return parts


def verify_zip(archive):
    with zipfile.ZipFile(archive) as z:
        entries = z.infolist()
        if len(entries) > 4000 or sum(x.file_size for x in entries) > 1024**3:
            raise ValueError('Archive resource limit')
        seen = set()
        for entry in entries:
            parts = safe_name(entry.filename)
            if PurePosixPath(entry.filename).suffix.lower() in PRIVATE | PERSONAL | {'.pdb'} or PurePosixPath(entry.filename).name.lower() == 'license-consent.json':
                raise ValueError('Forbidden private/data/debug entry')
            if entry.filename.casefold() in seen or len(parts) < 2 or entry.file_size > 64 * 1024**2:
                raise ValueError('Duplicate/root/oversized entry')
            seen.add(entry.filename.casefold())
            if stat.S_ISLNK(entry.external_attr >> 16):
                raise ValueError('Symlink entry')
        roots = {safe_name(x.filename)[0] for x in entries}
        if len(roots) != 1:
            raise ValueError('Multiple archive roots')
        prefix = roots.pop() + '/'
        manifest = json.loads(z.read(prefix + 'FILE_MANIFEST.json'))
        expected = {prefix + row['path'] for row in manifest['files']} | {prefix + 'FILE_MANIFEST.json'}
        if expected != {x.filename for x in entries}:
            raise ValueError('Manifest file set mismatch')
        for row in manifest['files']:
            safe_name(row['path'])
            payload = z.read(prefix + row['path'])
            if len(payload) != row['bytes'] or hashlib.sha256(payload).hexdigest().upper() != row['sha256']:
                raise ValueError('Manifest hash/length mismatch: ' + row['path'])
        if not REQUIRED <= {row['path'] for row in manifest['files']}:
            raise ValueError('Missing required runtime/resource/notice')
        return {'files': len(entries), 'uncompressedBytes': sum(x.file_size for x in entries),
                'compressedBytes': archive.stat().st_size, 'sha256': sha(archive),
                'allManifestHashesMatch': True, 'zipCRCValid': z.testzip() is None}


def collect_notices(destination, inventory, packages):
    rows = []
    identities = {(p['Id'], p['Version']): p for p in inventory['Packages'] + inventory.get('ImplicitRuntimePackages', []) if p['RuntimeFilesMatchedByHash']}
    runtime_versions = {f['version'] for f in inventory['RuntimeConfig']['runtimeOptions'].get('includedFrameworks', [])}
    sdk_versions = {p['Version'] for p in inventory['Packages'] if p['Id'].lower() in {'microsoft.windowsappsdk', 'microsoft.windowsappsdk.runtime'}}
    # Runtime packs / framework notices may not occur in the ordinary dependency graph.
    for name in ('microsoft.netcore.app.runtime.win-x64', 'microsoft.windowsdesktop.app.runtime.win-x64', 'microsoft.windowsappsdk', 'microsoft.windowsappsdk.runtime'):
        for folder in sorted((packages / name).glob('*')):
            if folder.name in (runtime_versions if name.startswith('microsoft.netcore.') or name.startswith('microsoft.windowsdesktop.') else sdk_versions):
                identities.setdefault((name, folder.name), {'Id': name, 'Version': folder.name, 'License': 'Runtime/framework; exact bundled texts', 'RuntimeFilesMatchedByHash': []})
    for (identity, version), package in sorted(identities.items()):
        folder = packages / identity.lower() / version.lower()
        if not folder.is_dir():
            raise FileNotFoundError('Package not found: ' + identity)
        copied = []
        for file in sorted(folder.rglob('*')):
            if not file.is_file() or not (file.suffix == '.nuspec' or any(m in file.name.lower() for m in NOTICE_MARKERS)):
                continue
            if file.suffix.lower() in PRIVATE or file.is_symlink():
                raise ValueError('Unsafe package notice')
            if file.suffix.lower() not in {'.txt', '.md', '.nuspec', ''} or file.stat().st_size > 4 * 1024**2:
                continue
            relative = file.relative_to(folder)
            target = destination / identity / version / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(file, target)
            copied.append({'path': target.relative_to(destination.parent).as_posix(), 'sha256': sha(target)})
        has_text = any(not row['path'].endswith('.nuspec') for row in copied)
        rows.append({'id': identity, 'version': version, 'licenseMetadata': package['License'],
                     'runtimeFiles': package['RuntimeFilesMatchedByHash'], 'copiedFiles': copied,
                     'hasPackageNoticeText': has_text, 'redistributionCleared': False})
    return rows


def source_commit(workspace):
    status = subprocess.run(['git', 'status', '--porcelain'], cwd=workspace, check=True, capture_output=True, text=True)
    if status.stdout.strip():
        raise ValueError('Build source workspace must be clean')
    return subprocess.run(['git', 'rev-parse', 'HEAD'], cwd=workspace, check=True, capture_output=True, text=True).stdout.strip()


def check_inventory(source, selected, inventory):
    rows = {r['Path']: r for r in inventory['Files']}
    for file in selected:
        name = file.relative_to(source).as_posix()
        row = rows.get(name)
        if row is None or row['Bytes'] != file.stat().st_size or row['SHA256'].upper() != sha(file):
            raise ValueError('Release does not match the supplied inventory: ' + name)
    if any(file.name.lower() in {'automapper.dll', 'sevenzipsharp.dll', '7za.exe', '7za.dll'} for file in selected):
        raise ValueError('Removed dependency unexpectedly reappeared')


def stage_verified_notices(workspace, destination):
    spec = importlib.util.spec_from_file_location('verified_notices', ROOT / 'scripts/verify_license_materials.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    result, files = module.verify(workspace)
    if destination.exists():
        raise FileExistsError('Refusing to overwrite a notice bundle')
    source = workspace / 'docs/licenses'
    for name in [*files, 'components/INDEX.json', 'REVIEW.json', 'README.md',
                 *('standard/' + x + '.txt' for x in ['Apache-2.0', 'MIT', 'BSD-3-Clause', 'MS-PL'])]:
        target = destination / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source / name, target)
    (destination / 'BUNDLE_STATUS.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    (destination / 'README.md').write_text('# Current binary notice materials\n\nSee components/INDEX.json for exact package versions and REVIEW.json for original source URLs, hashes and scope decisions. Supplemental files retain their original bytes. BUNDLE_STATUS.json records the remaining decisions; successful hashing is not redistribution permission. Historical SevenZipSharp/LGPL and AutoMapper materials are not part of this current bundle.\n', encoding='utf-8')
    return result


def runtime_requirements(inventory, packages):
    app_versions = {p['Version'] for p in inventory['Packages'] if p['Id'].lower() == 'microsoft.windowsappsdk'}
    runtime_versions = {p['Version'] for p in inventory['Packages'] if p['Id'].lower() == 'microsoft.windowsappsdk.runtime'}
    if len(app_versions) != 1 or runtime_versions != app_versions:
        raise ValueError('Ambiguous or inconsistent resolved Windows App SDK runtime')
    sdk_version = next(iter(app_versions))
    version = json.loads((packages / 'microsoft.windowsappsdk.runtime' / sdk_version / 'WindowsAppSDK-VersionInfo.json').read_text(encoding='utf-8-sig'))
    if version['Release']['Channel'] != 'stable':
        raise ValueError('An internal Preview app must still use the selected stable Windows App SDK')
    return {'windowsAppSdkNuGet': sdk_version,
            'frameworkPackageFamily': version['Runtime']['Packages']['Framework']['PackageFamilyName'],
            'minimumFrameworkVersion': version['Runtime']['Version']['String'],
            'releaseMajorMinor': version['Release']['MajorMinor']['HexUInt32'],
            'architecture': 'x64', 'projectMinimumWindowsBuild': 17763, 'testedWindowsBuild': 26200,
            'dotnetSelfContained': True, 'windowsAppSdkSelfContained': False, 'cleanMachineVerified': False}


def prepare(source, output, inventory, packages, source_workspace=None, version='0.1.0'):
    if not re.fullmatch(r'\d+\.\d+\.\d+', version):
        raise ValueError('Use a numeric major.minor.patch version')
    if output.exists():
        raise FileExistsError('Refusing to overwrite candidate directory')
    files = sorted(p for p in source.rglob('*') if p.is_file())
    selected, excluded = [], []
    # Validate all input names/types before creating output or opening private content.
    for file in files:
        if file.is_symlink() or file.is_junction() or any((source / p).is_symlink() or (source / p).is_junction() for p in file.relative_to(source).parents if p != Path('.')):
            raise ValueError('Linked source file')
        relative = file.relative_to(source)
        safe_name(relative.as_posix())
        reason = exclusion(relative)
        if reason:
            excluded.append({'path': relative.as_posix(), 'reason': reason, 'bytes': file.stat().st_size})
        else:
            selected.append(file)
    if not REQUIRED <= {p.relative_to(source).as_posix() for p in selected}:
        raise ValueError('Release input is incomplete')
    check_inventory(source, selected, inventory)
    workspace = (source_workspace or ROOT).resolve()
    if not source.resolve().is_relative_to(workspace / 'GalgameManager/bin'):
        raise ValueError('Build input must belong to the declared source workspace')
    head = source_commit(workspace)
    output.mkdir(parents=True)
    candidate = output / f'LemonVN-v{version}-preview-windows-x64-internal'
    candidate.mkdir()
    license_result = stage_verified_notices(workspace, candidate / 'licenses')
    for file in selected:
        target = candidate / file.relative_to(source)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(file, target)
    for filename in ('docs/INSTALLATION_GUIDE.md', 'docs/RELEASE_NOTES_0.1.md', 'docs/PRIVACY.md', 'docs/API_AND_LICENSE_RISKS.md',
                     'docs/SDK_DISTRIBUTION_REVIEW.md', 'docs/SDK_END_USER_TERMS_DRAFT.md'):
        shutil.copyfile(ROOT / filename, candidate / Path(filename).name)
    (candidate / 'README.md').write_text(f'# LemonVN {version} Windows x64 — 个人本地测试候选\n\n尚未完成本候选的用户验收，不是新的正式发行。完整解压后运行 LemonVN.exe；需要兼容的 Windows App Runtime x64，.NET 随包提供。详见 [运行指南](INSTALLATION_GUIDE.md)、[隐私](PRIVACY.md)、[候选说明](RELEASE_NOTES_0.1.md)及[第三方风险](API_AND_LICENSE_RISKS.md)。\n\n数据默认位于 %LocalAppData%\\LemonVN，ZIP 不是数据便携模式。启动、选择图片不会上传；确认后才向对应识别服务发送 JPEG。程序不提供视频播放、下载、资源扫描或账号同步。\n\n卸载此应用副本可运行同目录的 Uninstall.exe，默认保留本地数据。只有勾选删除数据并再次确认后才删除本地收藏、封面、设置、缓存与备份；请先把所需备份导出到其他位置。仅删除发行清单中的应用文件，保留目录内其他文件，不卸载系统运行时。需系统内置 Windows PowerShell；系统脚本策略或权限不允许时安全拒绝，不修改安全设置。\n\n基于 GoldenPotato137/PotatoVN 修改，原 LICENSE/NOTICE 保留。本候选状态不缩减 Apache-2.0 代码的商业使用许可；各第三方条款分别适用。未签名、未测试所有 Windows/DPI 或干净系统；不要绕过系统安全提示。\n', encoding='utf-8')
    with (candidate / 'README.md').open('a', encoding='utf-8') as stream:
        stream.write('\nLemonVN V0.1 Preview 已在现有 Windows x64 开发环境中验证运行，尚未完成全新 Windows 系统测试。首次启动可能需要安装兼容的 Windows App Runtime。\n\n许可材料与待决状态见 licenses/components/INDEX.json、licenses/REVIEW.json 和 licenses/BUNDLE_STATUS.json；不提供番剧视频资源、视频下载或在线播放服务。\n')
    shutil.copyfile(ROOT / 'scripts/Check-Runtime.ps1', candidate / 'Check-Runtime.ps1')
    requirement = runtime_requirements(inventory, packages)
    (candidate / 'runtime-requirements.json').write_text(json.dumps(requirement, indent=2) + '\n', encoding='utf-8')
    notices = json.loads((candidate / 'licenses/components/INDEX.json').read_text(encoding='utf-8-sig'))
    (candidate / 'INTERNAL_TEST_ONLY.txt').write_text('LemonVN V0.1 Preview personal/local research test candidate. NOT cleared for public distribution.\nAutoMapper and SevenZipSharp are absent from the current client payload. Remaining license/scope decisions: ' + str(len(license_result['pendingReleaseDecisions'])) + '; see licenses/BUNDLE_STATUS.json and licenses/REVIEW.json.\nDo not redistribute this candidate or upload it to GitHub Releases. This status does not restrict the commercial permissions of Apache-2.0 source code.\n', encoding='utf-8')
    manifest = {'application': 'LemonVN', 'version': version, 'scope': 'maintainer personal/local research only', 'publicDistributionCleared': False,
                'sourceCommit': head, 'buildSourceWorkingTreeClean': True,
                'packagingDocumentsMayIncludeUncommittedChanges': True,
                'createdAtUtc': datetime.now(timezone.utc).isoformat(), 'files': []}
    for file in sorted(candidate.rglob('*')):
        if file.is_file():
            manifest['files'].append({'path': file.relative_to(candidate).as_posix(), 'bytes': file.stat().st_size, 'sha256': sha(file)})
    (candidate / 'FILE_MANIFEST.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    archive = output / (candidate.name + '.zip')
    temporary = archive.with_suffix('.zip.partial')
    with zipfile.ZipFile(temporary, 'x', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for file in sorted(candidate.rglob('*')):
            if file.is_file():
                z.write(file, candidate.name + '/' + file.relative_to(candidate).as_posix())
    verification = verify_zip(temporary)
    if not verification['zipCRCValid']:
        raise ValueError('ZIP CRC check failed')
    temporary.rename(archive)
    archive.with_suffix('.zip.sha256').write_text(verification['sha256'] + '  ' + archive.name + '\n', encoding='ascii')
    report = {'archive': archive.relative_to(ROOT).as_posix(), **verification, 'excluded': excluded,
              'managedDllsRemoved': [], 'noticePackageCount': len(notices),
              'packagesMissingExactNoticeText': [r['id'] for r in notices if not r['hasPackageNoticeText']],
              'runtimeRequirements': requirement, 'publicDistributionCleared': False}
    report['sourceCommit'] = head
    report['licenseMaterials'] = license_result
    (output / 'candidate-report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, default=ROOT / 'GalgameManager/bin/x64/Release/net10.0-windows10.0.22621.0/win-x64')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--source-workspace', type=Path, default=ROOT)
    parser.add_argument('--inventory', type=Path, default=ROOT / 'docs/verification/M6B1-release-inventory.json')
    parser.add_argument('--internal-personal-test', action='store_true', required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    if not output.is_relative_to(ROOT / 'artifacts/internal') or output == ROOT / 'artifacts/internal':
        raise ValueError('Output must be a new subdirectory of artifacts/internal')
    report = prepare(args.source.resolve(), output, json.loads(args.inventory.read_text(encoding='utf-8-sig')), ROOT / '.tools/nuget-packages', args.source_workspace)
    print(json.dumps({k: v for k, v in report.items() if k not in {'excluded', 'packagesMissingExactNoticeText'}}, ensure_ascii=False, indent=2))
