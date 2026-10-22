"""Read-only audit of reachable Git metadata and selected text blobs.

Never reads certificate/key blobs. Findings contain locations/rules, never values.
This heuristic is not a guarantee that a repository is secret-free or licensed.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
PRIVATE_SUFFIXES = {'.pfx', '.p12', '.pem', '.key', '.snk', '.cer', '.crt'}
TEXT_SUFFIXES = {'.c', '.h', '.rc', '.cs', '.xaml', '.json', '.xml', '.config', '.props', '.targets', '.md', '.txt', '.ps1', '.py', '.sh', '.yml', '.yaml', '.env', '.toml', '.ini'}
RULES = {
    'github-token-shape': re.compile(rb'(?:gh[pousr]_[A-Za-z0-9]{36}|github_pat_[A-Za-z0-9_]{50,})'),
    'aws-access-id-shape': re.compile(rb'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b'),
    'private-key-header': re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----'),
    'literal-credential-field': re.compile(rb'''(?i)["'](?:access_token|client_secret|password|api_key)["']\s*[:=]\s*["'][A-Za-z0-9_+/.=-]{16,}["']'''),
}


def git(*args):
    return subprocess.run(['git', *args], cwd=ROOT, check=True, capture_output=True).stdout


def audit():
    paths = {}
    for line in git('rev-list', '--objects', 'HEAD').decode('utf-8').splitlines():
        oid, _, path = line.partition(' ')
        if path:
            paths.setdefault(oid, set()).add(path)
    # rev-list may show only one pathname for an object. Inspect tree metadata
    # for every reachable commit so a key renamed to .txt still stays blocked.
    for commit in git('rev-list', 'HEAD').decode().splitlines():
        for row in git('ls-tree', '-r', '-z', commit).split(b'\0'):
            if not row:
                continue
            meta, raw_path = row.split(b'\t', 1)
            path = raw_path.decode('utf-8')
            if Path(path).suffix.lower() in PRIVATE_SUFFIXES:
                oid = meta.decode().split()[2]
                paths.setdefault(oid, set()).add(path)
    blocked = {oid for oid, names in paths.items() if any(Path(n).suffix.lower() in PRIVATE_SUFFIXES for n in names)}
    tracked = []
    for row in git('ls-tree', '-r', '-z', 'HEAD').split(b'\0'):
        if row:
            meta, path = row.split(b'\t', 1)
            mode, kind, oid = meta.decode().split()
            tracked.append({'path': path.decode('utf-8'), 'kind': kind, 'oid': oid})
    findings, scanned, skipped_size = [], 0, 0
    candidates = [oid for oid, names in paths.items() if oid not in blocked and any(Path(n).suffix.lower() in TEXT_SUFFIXES for n in names)]
    metadata = subprocess.run(['git', 'cat-file', '--batch-check'], cwd=ROOT,
                              input=('\n'.join(candidates) + '\n').encode(), check=True, capture_output=True).stdout
    selected = []
    for row in metadata.decode().splitlines():
        oid, kind, size = row.split()
        if kind == 'blob' and int(size) <= 2 * 1024 * 1024:
            selected.append(oid)
        elif kind == 'blob':
            skipped_size += 1
    # A single Git reader avoids thousands of subprocesses. Private object IDs
    # are excluded before requesting any payload, including alternate path names.
    with subprocess.Popen(['git', 'cat-file', '--batch'], cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE) as reader:
        for oid in selected:
            reader.stdin.write((oid + '\n').encode())
            reader.stdin.flush()
            header = reader.stdout.readline().decode().split()
            if header[:2] != [oid, 'blob']:
                raise ValueError('Unexpected Git batch object')
            data = reader.stdout.read(int(header[2]))
            if reader.stdout.read(1) != b'\n':
                raise ValueError('Malformed Git batch boundary')
            if b'\0' in data[:8192]:
                continue
            scanned += 1
            for rule, pattern in RULES.items():
                matches = list(pattern.finditer(data))
                if matches:
                    findings.append({'object': oid, 'paths': sorted(paths[oid]), 'rule': rule,
                                     'lines': sorted({data.count(b'\n', 0, m.start()) + 1 for m in matches}),
                                     'matches': len(matches), 'valuesRecorded': False})
        reader.stdin.close()
        reader.wait(timeout=30)
        if reader.returncode:
            raise RuntimeError('Git reader failed')
    private = [{'object': oid, 'paths': sorted(paths[oid]), 'contentRead': False} for oid in sorted(blocked)]
    workflows = []
    for path in sorted((ROOT / '.github/workflows').glob('*.y*ml')):
        text = path.read_text(encoding='utf-8-sig')
        job_ids = re.findall(r'^  ([A-Za-z0-9_-]+):\s*$', text.split('jobs:', 1)[-1], re.M)
        guarded = re.findall(r"^    if:.*github\.repository == 'GoldenPotato137/PotatoVN'", text, re.M)
        workflows.append({'path': path.relative_to(ROOT).as_posix(), 'jobs': job_ids,
                          'guardCount': len(guarded), 'allJobsUpstreamGuarded': len(job_ids) == len(guarded),
                          'hasExternalWriteOrSigningSteps': bool(re.search(r'git push|gh release|msstore publish|api-token:|push: true|createComment', text))})
    return {'head': git('rev-parse', 'HEAD').decode().strip(),
            'commitCount': int(git('rev-list', '--count', 'HEAD')),
            'isShallow': git('rev-parse', '--is-shallow-repository').strip() == b'true',
            'trackedFiles': len(tracked), 'reachableObjectsWithPaths': len(paths),
            'privateMaterialMetadataOnly': private, 'privateBlobContentsRead': False,
            'trackedDataAndArchives': [r for r in tracked if Path(r['path']).suffix.lower() in {'.db', '.zip', '.log', '.7z', '.rar'}],
            'textBlobsScanned': scanned, 'oversizedTextBlobsSkipped': skipped_size,
            'findings': findings, 'workflows': workflows,
            'limitations': ['HEAD reachable shallow history only; unseen upstream ancestors not audited',
                            'Certificates/keys and binary blobs not inspected; .resw excluded by repository rules',
                            'Heuristic patterns can miss secrets; paths/objects are evidence, not legal clearance',
                            'Current job guards prevent execution in another repository; no workflows dispatched'],
            'hasPrivateMaterialInReachableHistory': bool(private),
            'readyToPushExistingHistory': False,  # A heuristic cannot grant publication clearance.
            'historyRewritten': False}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', type=Path, default=ROOT / 'docs/verification/M6B1-git-audit.json')
    args = parser.parse_args()
    result = audit()
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k not in {'findings', 'trackedDataAndArchives', 'workflows', 'privateMaterialMetadataOnly'}}, ensure_ascii=False, indent=2))
    print('Redacted finding groups:', len(result['findings']))
