"""Read-only source snapshot checks; never reads keys or .resw directly.

Run before the first commit and again with --tracked after it. Only redacted
locations are recorded. Heuristics cannot prove the absence of every secret.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

EXCLUDED = {'.git', 'bin', 'obj', '.vs', '.idea', '.tools', 'artifacts', 'testresults', '__pycache__'}
PRIVATE = {'.pfx', '.p12', '.pem', '.key', '.snk', '.cer', '.crt'}
DISALLOWED = {'.db', '.sqlite', '.sqlite3', '.log', '.zip', '.7z', '.rar', '.dll', '.exe', '.pdb', '.nupkg', '.ttf', '.otf'}
TEXT = {'.c', '.h', '.rc', '.cs', '.xaml', '.json', '.xml', '.config', '.props', '.targets', '.md', '.txt', '.rtf', '.ps1', '.py', '.sh', '.yml', '.yaml', '.env', '.toml', '.ini', '.sln', '.nuspec', '.manifest', '.appxmanifest', '.runsettings', ''}
RULES = {
    'github-token': re.compile(rb'(?:gh[pousr]_[A-Za-z0-9]{36}|github_pat_[A-Za-z0-9_]{50,})'),
    'aws-access-id': re.compile(rb'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b'),
    'private-key-header': re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----'),
    'literal-credential': re.compile(rb'''(?i)["'](?:access_token|client_secret|password|api_key)["']\s*[:=]\s*["'][A-Za-z0-9_+/.=-]{16,}["']'''),
    'personal-windows-path': re.compile(rb'(?i)[A-Z]:[\\/]+Users[\\/]+(?!Public\b|Default\b)[A-Za-z0-9_.-]+[\\/]'),
}
PUBLIC_ROOT = '5eb46a0d3530360773d3356602253368b11829ba'
PUBLIC_REMOTE = 'https://github.com/38huangyukong38/Lemonvn.git'
INITIAL_IMAGES = {'GalgameManager/Assets/LemonPlaceholder.png', 'GalgameManager/Assets/LemonPlaceholder.ico',
                  'docs/verification/M6B1-gui-second-home-light.jpg'}
APPROVED_ICON_IMAGES = {'GalgameManager/Assets/' + name for name in (
    'LemonAppIcon.ico', 'LemonAppIcon.png', 'LemonSquare150x150Logo.png',
    'LemonSquare44x44Logo.png', 'LemonSquare71x71Logo.png', 'LemonStoreLogo.png')}


def history_allowed(history, maintenance=False):
    if history['shallow'] or history['alternatesPresent'] or history['upstreamPfxObjectPresent']:
        return False
    if maintenance:
        return history['roots'] == [PUBLIC_ROOT] and history['remotes'] == ['origin'] and history['remoteUrls'] == [PUBLIC_REMOTE]
    return history['oneRootCommit'] and not history['remotes']


def run(root, *args, check=True):
    return subprocess.run(['git', *args], cwd=root, check=check, capture_output=True)


def audit(root, report_path, tracked, maintenance=False):
    root = root.resolve()
    if tracked:
        names = run(root, 'ls-files', '-z').stdout.decode().split('\0')
        files = [root / n for n in names if n]
    else:
        files = [p for p in root.rglob('*') if p.is_file() and not any(x.lower() in EXCLUDED for x in p.relative_to(root).parts)]
    findings, denied, links, scanned, images, oversized = [], [], [], 0, [], []
    for file in sorted(files):
        rel = file.relative_to(root).as_posix()
        if file.is_symlink() or file.is_junction() or not file.resolve().is_relative_to(root):
            links.append(rel)
            continue
        if file.suffix.lower() in PRIVATE | DISALLOWED or rel.startswith('.github/workflows/'):
            denied.append(rel)  # Do not read private material.
            continue
        if file.suffix.lower() == '.resw':
            continue  # Dedicated resource tool below owns ALL resource access.
        if file.suffix.lower() in {'.png', '.jpg', '.jpeg', '.ico', '.webp', '.gif', '.svg'}:
            images.append(rel)
        if file.suffix.lower() not in TEXT:
            continue
        if file.stat().st_size > 2 * 1024 * 1024:
            oversized.append(rel)
            continue
        data = file.read_bytes()
        if b'\0' in data[:8192]:
            continue
        scanned += 1
        for name, pattern in RULES.items():
            hits = list(pattern.finditer(data))
            if hits:
                findings.append({'path': rel, 'rule': name, 'lines': sorted({data.count(b'\n', 0, m.start()) + 1 for m in hits}), 'valuesRecorded': False})
    resource_report = report_path.with_name(report_path.stem + '-resources.json')
    subprocess.run([sys.executable, str(root/'GalgameManager/Strings/resw_tool.py'), '--dir', str(root/'GalgameManager/Strings'), 'audit-snapshot', '--report', str(resource_report)], check=True, cwd=root)
    resources = json.loads(resource_report.read_text(encoding='utf-8-sig'))
    expected_images = INITIAL_IMAGES | APPROVED_ICON_IMAGES if maintenance else INITIAL_IMAGES
    history = None
    if tracked:
        root_row = run(root, 'rev-list', '--parents', 'HEAD').stdout.decode().splitlines()
        history = {'commitCount': len(root_row), 'oneRootCommit': len(root_row) == 1 and len(root_row[0].split()) == 1,
                   'remotes': run(root, 'remote').stdout.decode().splitlines(),
                   'shallow': run(root, 'rev-parse', '--is-shallow-repository').stdout.strip() == b'true',
                   'alternatesPresent': (root/'.git/objects/info/alternates').exists(),
                   'upstreamPfxObjectPresent': run(root, 'cat-file', '-e', '571bfa157e078765a92e9a1dcee1cac7988ebf78', check=False).returncode == 0,
                   'roots': run(root, 'rev-list', '--max-parents=0', 'HEAD').stdout.decode().splitlines(),
                   'remoteUrls': run(root, 'remote', 'get-url', '--all', 'origin', check=False).stdout.decode().splitlines(),
                   'head': run(root, 'rev-parse', 'HEAD').stdout.decode().strip()}
    result = {'sourceFiles': len(files), 'textFilesScanned': scanned, 'findings': findings, 'disallowedPaths': denied,
              'linkedPaths': links, 'oversizedTextSkipped': oversized, 'images': sorted(images),
              'onlyReviewedImages': set(images) == expected_images, 'maintenanceMode': maintenance, 'resourcesReport': resource_report.name,
              'resourceFindings': resources.get('findings', []), 'history': history,
              'allClientTestSourceFiles': len([p for p in files if p.relative_to(root).as_posix().startswith('GalgameManager.Test/') and p.suffix == '.cs']),
              'limitations': ['Pattern checks are not a guarantee against all secrets.', 'Binary image rights reviewed separately; no image-content secret detection.', 'Ignored build outputs/toolchain are outside the proposed source commit.', 'No publication or commercial/API authorization is granted by this report.']}
    result['passed'] = not (findings or denied or links or oversized or result['resourceFindings']) and result['onlyReviewedImages']
    if tracked:
        result['passed'] &= history_allowed(history, maintenance)
    report_path.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in {'findings','images','limitations'}}, ensure_ascii=False, indent=2))
    if not result['passed']:
        raise SystemExit('Snapshot check requires review; values were not printed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--tracked', action='store_true')
    parser.add_argument('--maintenance', action='store_true', help='Require the existing independent public root and exact origin; allow maintainer-approved icons')
    args = parser.parse_args()
    audit(args.root, args.report.resolve(), args.tracked, args.maintenance)
