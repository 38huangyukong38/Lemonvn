"""Prepare a local Preview asset from a clean reviewed source; never publish it."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import re

from native_launcher import inspect_launcher
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


def prepare(source, output, inventory, packages, version='0.1.0', revision=None, launcher=None):
    if revision is not None and not re.fullmatch(r'r[1-9][0-9]*', revision):
        raise ValueError('Use a numeric package revision such as r2')
    if revision is not None:
        launcher = Path(launcher or ROOT / 'Launcher/bin/x64/Release/LemonVN.exe').absolute()
        if not launcher.resolve().is_relative_to(ROOT / 'Launcher/bin') or any(
            p.is_symlink() or p.is_junction() for p in [launcher, *launcher.parents]):
            raise ValueError('Use the owned launcher build output')
        launcher_audit = inspect_launcher(launcher)
        receipt = json.loads((launcher.parent / 'build-receipt.json').read_text(encoding='utf-8-sig'))
        if (receipt.get('compiler') != 'Zig0.15.2' or receipt.get('productVersion') != version + '.0' or
            receipt.get('binarySha256') != launcher_audit['sha256'] or
            receipt.get('sourceSha256') != internal.sha(ROOT / 'Launcher/launcher.c') or
            receipt.get('manifestSha256') != internal.sha(ROOT / 'Launcher/launcher.manifest') or
            receipt.get('iconSha256') != internal.sha(ROOT / 'GalgameManager/Assets/LemonAppIcon.ico')):
            raise ValueError('Native launcher build receipt does not match reviewed source')
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
    suffix = '-' + revision if revision else ''
    candidate = output / f'LemonVN-v{version}-preview{suffix}-windows-x64'
    stage = output / f's/LemonVN-v{version}-preview-windows-x64-internal'
    candidate.mkdir()
    payload = candidate / 'app' if revision else candidate
    payload.mkdir(exist_ok=True)
    for path in stage.rglob('*'):
        if not path.is_file() or path.name in {'FILE_MANIFEST.json', 'INTERNAL_TEST_ONLY.txt'} or (revision and path.relative_to(stage).as_posix() == 'Uninstall.exe'):
            continue
        target = payload / path.relative_to(stage)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, target)
    for name in ['MAINTAINER_CONFIRMATIONS.json', '2026-10-17-draft2-approved-source.md']:
        target = payload / 'docs/release-review' / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(ROOT / 'docs/release-review' / name, target)
    (payload / 'README.md').write_text(
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
                'buildSourceWorkingTreeClean': True, 'preparedForTag': 'v'+version+'-preview'+suffix,
                'identifiedDistributionConditionsAddressed': True, 'legalCertification': False,
                'publicationPerformed': False, 'publicationAuthorizedAtPreparation': False,
                'createdAtUtc': datetime.now(timezone.utc).isoformat(), 'files': []}
    if revision:
        manifest['revision'] = revision
        manifest['layout'] = 'app-subdirectory-v1'
        manifest['nativeLauncher'] = launcher_audit
        for name in ['LemonVN.exe', 'Uninstall.exe']:
            shutil.copyfile(launcher, candidate / name)
        for name in ['LICENSE', 'NOTICE']:
            shutil.copyfile(payload / name, candidate / name)
        docs = candidate / 'docs'
        docs.mkdir()
        for name in ['INSTALLATION_GUIDE.md', 'RELEASE_NOTES_0.1.md', 'PRIVACY.md', 'API_AND_LICENSE_RISKS.md']:
            shutil.copyfile(payload / name, docs / name)
        (candidate / 'README.md').write_text(
            f'# LemonVN v{version} Preview ({revision})\n\n'
            '运行 LemonVN.exe；卸载运行 Uninstall.exe。必须完整解压，保留 app 与 docs 文件夹。\n'
            'app 保存程序和运行库，请勿单独移动或删除其中的文件。\n\n'
            '安装、升级与运行时要求：[运行说明](docs/INSTALLATION_GUIDE.md)。'
            '本次变更：[更新说明](docs/RELEASE_NOTES_0.1.md)。\n'
            '仅 Windows x64；内含.NET10，可能需要兼容Windows App Runtime。未签名、未做干净系统测试。\n'
            '默认数据位于 %LocalAppData%\\LemonVN，不是数据便携模式；升级先备份并正常关闭旧版。\n'
            '卸载默认保留数据；永久删除本地收藏等内容必须勾选并二次确认，先将必要备份导出到其他位置。\n'
            '免费开源，不提供视频播放、下载或视频资源。保留GoldenPotato137/PotatoVN署名。\n'
            '完整第三方许可与组件条款保留在 app/licenses、app/SDK_END_USER_TERMS_DRAFT.md；'
            '首次启动明确同意机制不变。\n', encoding='utf-8')
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
        shutil.copyfile(payload / name, output / name)
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
    parser.add_argument('--revision')
    parser.add_argument('--launcher',type=Path)
    args=parser.parse_args()
    output=args.output.resolve()
    if not output.is_relative_to(ROOT/'artifacts') or output==ROOT/'artifacts':
        raise ValueError('Use a fresh local artifacts subdirectory')
    print(json.dumps(prepare(args.source.resolve(),output,json.loads(args.inventory.read_text(encoding='utf-8-sig')),args.packages.resolve(),args.version,args.revision,args.launcher),indent=2))
