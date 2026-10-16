"""Offline inventory of the resolved client graph and an unpackaged build.

NuGet metadata is evidence, not a redistribution clearance. No network or user
collection is read. Paths in the report are relative to the workspace/package.
"""
import argparse
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def inspect(workspace, output):
    assets = json.loads((workspace / 'GalgameManager/obj/project.assets.json').read_text(encoding='utf-8-sig'))
    package_roots = [Path(folder) for folder in assets['packageFolders']]
    target_name = next(name for name in assets['targets'] if name.endswith('/win-x64'))
    target = assets['targets'][target_name]
    files = sorted(path for path in output.rglob('*') if path.is_file())
    file_rows = [{'Path': path.relative_to(output).as_posix(), 'Bytes': path.stat().st_size, 'SHA256': sha(path)} for path in files]
    by_name = {}
    for row in file_rows:
        by_name.setdefault(Path(row['Path']).name.lower(), []).append(row)
    direct = {}
    for project in ('GalgameManager', 'GalgameManager.Core', 'GalgameManager.WinApp.Base'):
        tree = ET.parse(workspace / project / (project + '.csproj'))
        for reference in tree.findall('.//PackageReference'):
            direct.setdefault(reference.attrib['Include'].lower(), []).append(project)
    packages = []
    for key, library in sorted(assets['libraries'].items(), key=lambda pair: pair[0].lower()):
        if library['type'] != 'package':
            continue
        identity, version = key.rsplit('/', 1)
        package = next((root / library['path'] for root in package_roots if (root / library['path']).is_dir()), None)
        if package is None:
            raise FileNotFoundError('Missing package: ' + key)
        spec = next(package.glob('*.nuspec'))
        metadata = ET.parse(spec).getroot().find('{*}metadata')
        license_node = metadata.find('{*}license')
        license_type = license_node.attrib.get('type', '') if license_node is not None else 'legacy-url/unknown'
        license_value = (license_node.text or '').strip() if license_node is not None else metadata.findtext('{*}licenseUrl', '')
        repository = metadata.find('{*}repository')
        notices = []
        for path in package.iterdir():
            if path.is_file() and any(marker in path.name.lower() for marker in ('license', 'notice', 'copying', 'copyright')):
                notices.append({'Path': path.name, 'SHA256': sha(path)})
        if license_type == 'file':
            license_file = (package / license_value.replace('\\', '/')).resolve()
            if not license_file.is_relative_to(package.resolve()):
                raise ValueError('License path escapes package: ' + key)
            if not license_file.is_file():
                raise FileNotFoundError('Missing declared license: ' + key)
            if not any(row['Path'] == license_value for row in notices):
                notices.append({'Path': license_value, 'SHA256': sha(license_file)})
        matched = set()
        resolved = target.get(key, {})
        runtime_assets = set()
        for group in ('runtime', 'native', 'runtimeTargets'):
            runtime_assets.update(resolved.get(group, {}))
        for asset in runtime_assets:
            source = package / asset
            candidates = by_name.get(Path(asset).name.lower(), [])
            if candidates and source.is_file():
                digest = sha(source)
                matched.update(row['Path'] for row in candidates if row['SHA256'] == digest)
        risk = 'Metadata only; obligations must be reviewed'
        if identity.lower() == 'automapper' and int(version.split('.')[0]) >= 15:
            risk = 'RPL-1.5 or Lucky Penny license path unresolved'
        elif 'LGPL' in license_value.upper():
            risk = 'LGPL redistribution/source/relinking obligations require review'
        elif license_type == 'legacy-url/unknown':
            risk = 'Exact license text/version requires separate verification'
        packages.append({'Id': identity, 'Version': version, 'DirectIn': direct.get(identity.lower(), []),
                         'LicenseType': license_type, 'License': license_value,
                         'ProjectUrl': metadata.findtext('{*}projectUrl', ''),
                         'Repository': repository.attrib if repository is not None else {},
                         'LocalNotices': notices, 'RuntimeFilesMatchedByHash': sorted(matched), 'Review': risk})
    runtime_config = json.loads((output / 'LemonVN.runtimeconfig.json').read_text(encoding='utf-8-sig'))
    original = subprocess.run(['git', 'show', '2adef493ea0d82d27d4037e91507e558c567305d:LICENSE'],
                              cwd=workspace, capture_output=True)
    normalized_license = (workspace / 'LICENSE').read_bytes().replace(b'\r\n', b'\n')
    if original.returncode == 0:
        license_preserved = normalized_license == original.stdout.replace(b'\r\n', b'\n')
        license_check_mode = 'Original development Git baseline'
    else:
        provenance = json.loads((workspace / 'UPSTREAM_PROVENANCE.json').read_text(encoding='utf-8-sig'))
        license_preserved = hashlib.sha256(normalized_license).hexdigest() == provenance['upstreamLicenseNormalizedSha256']
        license_check_mode = 'Independent source provenance hash; old Git objects not required'
    extensions = Counter()
    for row in file_rows:
        extensions[Path(row['Path']).suffix.lower()] += row['Bytes']
    return {'Target': target_name, 'NuGetPackageCount': len(packages), 'Packages': packages,
            'RuntimeConfig': runtime_config, 'Files': file_rows, 'OutputBytes': sum(row['Bytes'] for row in file_rows),
            'BytesWithoutPdb': sum(row['Bytes'] for row in file_rows if not row['Path'].lower().endswith('.pdb')),
            'LargestFiles': sorted(file_rows, key=lambda row: row['Bytes'], reverse=True)[:25],
            'BytesByExtension': dict(extensions.most_common()),
            'UpstreamLicenseContentPreserved': license_preserved, 'UpstreamLicenseCheckMode': license_check_mode,
            'UnexpectedPrivateArtifacts': [row['Path'] for row in file_rows if Path(row['Path']).suffix.lower() in ('.pfx', '.p12', '.key')
                                           or Path(row['Path']).name.lower() in ('lemonvn.db', 'pvn_data.db', 'lemonvn-log.db')]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True, help='Local unpackaged build directory')
    parser.add_argument('--report', type=Path, default=Path('docs/verification/M6A-release-inventory.json'))
    args = parser.parse_args()
    workspace = Path(__file__).resolve().parents[1]
    result = inspect(workspace, args.output.resolve())
    args.report.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    rows = ['# Third-party dependency metadata inventory', '',
            'M6-A audit evidence, not redistribution approval. Includes build-time packages; runtime presence is checked by file hash.', '',
            '| Package | Version | NuGet license metadata | Direct reference | Runtime matches |',
            '| --- | --- | --- | --- | --- |']
    for package in result['Packages']:
        license_text = package['License'].replace('|', '\\|') or 'Not declared'
        rows.append(f"| {package['Id']} | {package['Version']} | {package['LicenseType']}: {license_text} | {', '.join(package['DirectIn']) or 'transitive'} | {len(package['RuntimeFilesMatchedByHash'])} |")
    rows.extend(['', 'Exact package notice paths/hashes, matched runtime files, source repositories and unresolved review flags:',
                 '[M6A-release-inventory.json](verification/M6A-release-inventory.json).', '',
                 'License files/URLs may refer to further conditions. Native bundled code, runtime packs, data and artwork require separate review.',
                 'See [API_AND_LICENSE_RISKS.md](API_AND_LICENSE_RISKS.md) before any distribution.'])
    (workspace / 'docs/THIRD_PARTY_LICENSES.md').write_text('\n'.join(rows) + '\n', encoding='utf-8')
    print(json.dumps({key: result[key] for key in ('NuGetPackageCount', 'OutputBytes', 'BytesWithoutPdb', 'UpstreamLicenseContentPreserved', 'UnexpectedPrivateArtifacts')}))


if __name__ == '__main__':
    main()
