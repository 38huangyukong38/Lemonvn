"""Verify versioned notice bytes and optionally stage a source-free notice bundle.

Mechanical verification does not grant redistribution clearance. Review statuses
are explicit human/audit decisions; missing package-body files are not inferred
to mean missing permission, nor is SPDX metadata treated as sufficient by itself.
"""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil


def checked_file(root, relative, expected):
    relative = PurePosixPath(relative)
    if relative.is_absolute() or '..' in relative.parts or '\\' in str(relative) or ':' in str(relative):
        raise ValueError('Unsafe notice path')
    target = root.joinpath(*relative.parts)
    if target.is_symlink() or not target.resolve().is_relative_to(root.resolve()) or not target.is_file():
        raise ValueError('Missing or linked notice')
    if target.suffix.lower() not in {'.txt', '.md', '.json', '.rtf', '.nuspec', ''}:
        raise ValueError('Unexpected notice format')
    if hashlib.sha256(target.read_bytes()).hexdigest().upper() != expected.upper():
        raise ValueError('Notice content does not match the audited hash')
    return target


def verify(workspace):
    folder = workspace / 'docs/licenses'
    index = json.loads((folder / 'components/INDEX.json').read_text(encoding='utf-8-sig'))
    review = json.loads((folder / 'REVIEW.json').read_text(encoding='utf-8-sig'))
    identities = {(x['id'], x['version']) for x in index}
    if len(identities) != len(index) or any(x[0].lower() == 'squid-box.sevenzipsharp' for x in identities):
        raise ValueError('Unexpected current component identities')
    paths = {}
    for item in index:
        for row in item['copiedFiles']:
            checked_file(folder, row['path'], row['sha256'])
            paths[row['path']] = row['sha256']
    pending = []
    seen = set()
    for item in review['historicalGaps'] + review.get('additionalCases', []):
        identity = (item['id'], item['version'])
        if identity not in identities or identity in seen or item['reviewStatus'] not in {'notice-materials-covered', 'needs-licensor-or-scope-confirmation'}:
            raise ValueError('Invalid review identity/status')
        seen.add(identity)
        if item['reviewStatus'] != 'notice-materials-covered':
            pending.append(item['id'])
        for row in item['sourceFiles']:
            checked_file(folder, row['path'], row['sha256'])
            if row['path'] in paths and paths[row['path']] != row['sha256']:
                raise ValueError('Conflicting notice bytes')
            paths[row['path']] = row['sha256']
    if len(seen) != review['historicalPackageBodyGapCount'] + len(review.get('additionalCases', [])):
        raise ValueError('Review count does not match its records')
    return {'components': len(index), 'verifiedNoticeFiles': len(paths),
            'historicalGaps': review['historicalPackageBodyGapCount'],
            'additionalCurrentComponentCases': len(review.get('additionalCases', [])),
            'coveredNoticeMaterialCases': len(seen)-len(pending),
            'pendingReleaseDecisions': pending, 'materialsConsistent': True,
            'allBinaryLicenseQuestionsResolved': not pending}, paths


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--workspace', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--report', type=Path)
    parser.add_argument('--stage-directory', type=Path)
    args = parser.parse_args()
    result, files = verify(args.workspace.resolve())
    if args.stage_directory:
        destination = args.stage_directory.resolve()
        if destination.exists():
            raise FileExistsError('Refusing to overwrite a notice bundle')
        destination.mkdir(parents=True)
        source = args.workspace / 'docs/licenses'
        for name in files:
            target = destination / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source / name, target)
        for name in ['components/INDEX.json', 'REVIEW.json', 'README.md']:
            target = destination / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source / name, target)
        for name in ['Apache-2.0', 'MIT', 'BSD-3-Clause', 'MS-PL']:
            target = destination / 'standard' / (name+'.txt')
            target.parent.mkdir(exist_ok=True)
            shutil.copyfile(source/'standard'/(name+'.txt'), target)
        (destination/'BUNDLE_STATUS.json').write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    if args.report:
        args.report.write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
