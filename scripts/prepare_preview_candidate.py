"""Prepare a local Preview asset from a clean reviewed source; never publish it."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET
import zipfile

import prepare_internal_candidate as internal

ROOT = Path(__file__).resolve().parents[1]


def verify_maintainer_confirmations(workspace):
    record = json.loads((workspace / 'docs/release-review/MAINTAINER_CONFIRMATIONS.json').read_text(encoding='utf-8-sig'))
    required = {'windowsSdkPublisherResponsibilities', 'windowsAppSdkAndWindowsMlPublisherResponsibilities',
                'newCodeCopyrightAttribution', 'suppliedIconPublicDistributionRights',
                'draft2TermsAndExplicitFirstLaunchMechanism'}
    if not required <= record.get('confirmations', {}).keys() or not all(record['confirmations'][k] is True for k in required):
        raise ValueError('An explicit maintainer scope confirmation is missing')
    terms = workspace / 'docs/SDK_END_USER_TERMS_DRAFT.md'
    if internal.sha(terms) != record['currentTermsSha256']:
        raise ValueError('Terms differ from the reviewed confirmation record')
    previous = workspace / 'docs/release-review/2026-10-17-draft2-approved-source.md'
    if internal.sha(previous) != record['approvedDraft2Sha256']:
        raise ValueError('Approved draft2 evidence differs')
    def sections(file):
        text = file.read_text(encoding='utf-8-sig').replace('\r\n', '\n')
        return text[text.index('## 1.'):text.index('## 6.')]
    if sections(terms) != sections(previous):
        raise ValueError('Substantive component terms changed; ask the maintainer before proceeding')
    return record


def prepare(source, output, inventory, packages, version='0.1.0'):
    if output.exists():
        raise FileExistsError('Refusing to overwrite a prepared Preview')
    confirmations = verify_maintainer_confirmations(ROOT)
    identity = ET.parse(ROOT/'GalgameManager/Package.appxmanifest').getroot().find('{*}Identity')
    if identity is None or identity.get('Version') != version+'.0':
        raise ValueError('Candidate version does not match the application manifest')
    # Reuse all existing source, payload, original-notice and secret-file checks.
    internal_report = internal.prepare(source, output / 's', inventory, packages, ROOT, version=version)
    if internal_report['licenseMaterials']['pendingReleaseDecisions']:
        raise ValueError('An identified component distribution decision is still unresolved')
    source_commit = internal_report['sourceCommit']
    candidate = output / f'LemonVN-v{version}-preview-windows-x64'
    stage = output / f's/LemonVN-v{version}-preview-windows-x64-internal'
    candidate.mkdir()
    for path in stage.rglob('*'):
        if not path.is_file() or path.name in {'FILE_MANIFEST.json', 'INTERNAL_TEST_ONLY.txt'}:
            continue
        target = candidate / path.relative_to(stage)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, target)
    for name in ['MAINTAINER_CONFIRMATIONS.json', '2026-10-17-draft2-approved-source.md']:
        target = candidate / 'docs/release-review' / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(ROOT / 'docs/release-review' / name, target)
    (candidate / 'README.md').write_text(
        f'# LemonVN v{version}-preview Windows x64\n\n'
        '免费开源番剧收藏与观看记录工具；不提供番剧视频播放、下载或视频资源。\n\n'
        '完整解压后运行 LemonVN.exe；无需管理员权限、Visual Studio或开发SDK。\n'
        '.NET10运行时已包含；可能需要兼容Windows App Runtime x64，最低2.4.0.0。\n'
        '详见[安装运行](INSTALLATION_GUIDE.md)、[中文发行说明](RELEASE_NOTES_0.1.md)、[隐私](PRIVACY.md)。\n\n'
        '尚未完成干净Windows系统测试，软件未正式代码签名，Windows可能显示安全提示。\n'
        '校验值不能代替受信任来源或签名；不要关闭安全防护或绕过系统提示。\n\n'
        '默认数据在 %LocalAppData%\\LemonVN，ZIP不是数据便携模式。升级前先备份、正常关闭旧程序。\n'
        '卸载请正常关闭程序后运行同目录 Uninstall.exe，默认保留本地数据。只有勾选删除数据并再次确认才永久移除收藏等本地内容；先将所需备份导出到其他位置。\n'
        '需要系统 Windows PowerShell；策略/权限阻止时安全拒绝，不提升权限或修改安全设置，不删除系统运行时或未列入清单的应用目录文件。\n\n'
        '首次明确同意前不打开收藏数据库；许可正文沿用SDK_END_USER_TERMS_DRAFT.md历史文件名，'
        '内容为维护者确认的draft2组件条款。Apache代码、MIT运行库和微软组件分别适用各自许可。\n\n'
        '保留GoldenPotato137/PotatoVN原作者署名及LICENSE/NOTICE。完整原文在licenses目录。\n'
        '公开源码：https://github.com/38huangyukong38/Lemonvn\n', encoding='utf-8')
    manifest = {'application': 'LemonVN', 'version': version+'-preview', 'sourceCommit': source_commit,
                'buildSourceWorkingTreeClean': True, 'preparedForTag': 'v'+version+'-preview',
                'identifiedDistributionConditionsAddressed': True, 'legalCertification': False,
                'publicationPerformed': False, 'publicationAuthorizedAtPreparation': False,
                'createdAtUtc': datetime.now(timezone.utc).isoformat(), 'files': []}
    for path in sorted(candidate.rglob('*')):
        if path.is_file():
            manifest['files'].append({'path': path.relative_to(candidate).as_posix(),
                                      'bytes': path.stat().st_size, 'sha256': internal.sha(path)})
    (candidate / 'FILE_MANIFEST.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    archive = output / (candidate.name + '.zip')
    with zipfile.ZipFile(archive.with_suffix('.zip.partial'), 'x', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for path in sorted(candidate.rglob('*')):
            if path.is_file():
                z.write(path, candidate.name+'/'+path.relative_to(candidate).as_posix())
    checked = internal.verify_zip(archive.with_suffix('.zip.partial'))
    if not checked['zipCRCValid']:
        raise ValueError('ZIP CRC failure')
    archive.with_suffix('.zip.partial').rename(archive)
    archive.with_suffix('.zip.sha256').write_text(checked['sha256']+'  '+archive.name+'\n',encoding='ascii')
    for name in ['RELEASE_NOTES_0.1.md', 'INSTALLATION_GUIDE.md']:
        shutil.copyfile(candidate / name, output / name)
    report = {**checked, 'archive': archive.relative_to(ROOT).as_posix(), 'sourceCommit':source_commit,
              'runtimeRequirements':internal_report['runtimeRequirements'],
              'licenseMaterials':internal_report['licenseMaterials'],
              'publicationAuthorized':False, 'publicationPerformed':False, 'legalCertification':False,
              'approvalBasis':'Explicit maintainer statements, not a signature or reading attestation'}
    (output/'preview-report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    return report


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--inventory',type=Path,required=True)
    parser.add_argument('--packages',type=Path,required=True)
    parser.add_argument('--version',default='0.1.0')
    args=parser.parse_args()
    output=args.output.resolve()
    if not output.is_relative_to(ROOT/'artifacts') or output==ROOT/'artifacts':
        raise ValueError('Use a fresh local artifacts subdirectory')
    print(json.dumps(prepare(args.source.resolve(),output,json.loads(args.inventory.read_text(encoding='utf-8-sig')),args.packages.resolve(),args.version),indent=2))
