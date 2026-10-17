"""Create an external, source-only review snapshot without copying Git history.

This script never initializes a remote, pushes, signs, publishes or rewrites the
development repository. A separate reviewed local commit is a later explicit step.
"""
import argparse
import codecs
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
BASE = '2adef493ea0d82d27d4037e91507e558c567305d'
PROJECTS = ('GalgameManager','GalgameManager.Core','GalgameManager.WinApp.Base','GalgameManager.Test')
SKIP_DIRS = {'.git','bin','obj','.vs','.idea','node_modules','testresults','appPackages','build','.tools','artifacts'}
PRIVATE = {'.pfx','.p12','.pem','.key','.snk','.cer','.crt'}
BINARY = {'.dll','.exe','.pdb','.db','.sqlite','.sqlite3','.zip','.7z','.rar','.log','.bak','.ttf','.otf','.webp','.jpg','.jpeg','.png','.ico','.nupkg','.pyc'}
OWN = {'GalgameManager/Assets/LemonPlaceholder.png','GalgameManager/Assets/LemonPlaceholder.ico'}
SCREENSHOT = 'docs/verification/M6B1-gui-second-home-light.jpg'
DOCS = ('README.md','LICENSE','NOTICE','THIRD_PARTY_NOTICES.md','NuGet.Config','Directory.Build.props','.editorconfig',
        'docs/INSTALLATION_GUIDE.md','docs/PRIVACY.md','docs/RELEASE_NOTES_0.1.md','docs/THIRD_PARTY_COMPONENTS.md',
        'docs/THIRD_PARTY_LICENSES.md')
SCRIPTS = ('scripts/Build-Release.ps1','scripts/Check-Runtime.ps1','scripts/runtime-requirements.json','scripts/audit_release.py','scripts/audit_source_snapshot.py',
           'scripts/verify_license_materials.py','scripts/tests/test_license_materials.py')
PUBLIC_DOCS = ('IMPLEMENTATION_LOG.md','GITHUB_OPEN_SOURCE_AUDIT.md','RELEASE_CHECKLIST.md',
               'API_AND_LICENSE_RISKS.md','M6_B2A_OPEN_SOURCE_CLEARANCE.md','ARCHITECTURE.md','M6_B2B1_LICENSE_AND_DEPENDENCY_CLEARANCE.md')


def git(*args):
    return subprocess.run(['git',*args],cwd=ROOT,check=True,capture_output=True).stdout


def digest(path):
    if path.suffix.lower()=='.resw':
        raise ValueError('Resource hashes must come from resw_tool.py')
    return hashlib.sha256(path.read_bytes()).hexdigest()


def mark_change(path):
    raw=path.read_bytes(); bom=raw.startswith(codecs.BOM_UTF8)
    text=raw.decode('utf-8-sig'); nl='\r\n' if '\r\n' in text else '\n'
    note='LemonVN modification from the fixed PotatoVN baseline; see LICENSE, NOTICE and UPSTREAM_PROVENANCE.json.'
    if note in text: return False
    if path.suffix.lower() in {'.cs','.py','.ps1','.props','.targets','.csproj','.xaml','.xml','.config','.runsettings','.appxmanifest','.manifest'}:
        if path.suffix.lower() in {'.cs','.py','.ps1'}:
            marker=('// ' if path.suffix.lower()=='.cs' else '# ')+note+nl
            text=marker+text
        else:
            marker='<!-- '+note+' -->'
            at=text.index('?>')+2 if text.startswith('<?xml') else 0
            text=text[:at]+nl+marker+nl+text[at:]
    elif path.suffix.lower()=='.json':
        data=json.loads(text)
        if not isinstance(data,dict):
            raise ValueError('Modified non-object JSON needs individual attribution review: '+str(path))
        # Configuration/schema metadata, never an Anime database/backup document.
        data['_lemonvnModificationNotice']=note
        text=json.dumps(data,ensure_ascii=False,indent=2)+nl
    elif path.suffix.lower()=='.md':
        text='> '+note+nl+nl+text
    else:
        return False
    path.write_bytes((codecs.BOM_UTF8 if bom else b'')+text.encode('utf-8'))
    return True


def export(destination):
    destination=destination.resolve()
    if not destination.is_absolute() or destination==ROOT or destination.is_relative_to(ROOT) or ROOT.is_relative_to(destination):
        raise ValueError('Snapshot must be a separate external directory')
    marker=destination/'SNAPSHOT_PREPARATION.txt'
    if not destination.is_dir() or sorted(p.name for p in destination.iterdir())!=[marker.name] or not marker.read_text().startswith('LemonVN M6-B2-A local review snapshot;'):
        raise ValueError('Expected an empty, explicitly prepared snapshot directory; refusing overwrite')
    changed=set(git('diff','--name-only',BASE).decode('utf-8').splitlines())
    upstream=set(git('ls-tree','-r','--name-only',BASE).decode('utf-8').splitlines())
    selected=[]; excluded=[]
    # Do not pick up ignored developer configuration or arbitrary untracked files.
    source_paths=git('ls-files','-z','--',*PROJECTS).decode('utf-8').split('\0')
    source_paths.append('GalgameManager.Test/Services/LegacyCharacterMappingTest.cs')
    for relative in sorted(set(source_paths)-{''}):
        file=ROOT/relative; reason=None
        if file.suffix.lower() in PRIVATE: reason='Signing/private material excluded without reading'
        elif any(part.lower() in {x.lower() for x in SKIP_DIRS} for part in Path(relative).parts): reason='Build/developer directory'
        elif relative.startswith('GalgameManager/Assets/') and relative not in OWN: reason='Inactive upstream asset excluded'
        elif file.suffix.lower() in BINARY and relative not in OWN: reason='Build, data or unreviewed binary material'
        elif file.name.lower()=='readme.md': reason='Obsolete project README; independent root guide replaces it'
        if reason:
            excluded.append({'path':relative,'reason':reason}); continue
        if not file.is_file() or not file.resolve().is_relative_to(ROOT.resolve()) or file.is_symlink() or file.is_junction():
            raise ValueError('Missing or linked source item: '+relative)
        selected.append(relative)
    for name in DOCS+SCRIPTS:
        if not (ROOT/name).is_file(): raise FileNotFoundError(name)
        selected.append(name)
    selected.append(SCREENSHOT)
    selected.extend(p.relative_to(ROOT).as_posix() for p in (ROOT/'docs/licenses').rglob('*') if p.is_file() and p.suffix.lower() in {'.txt','.md','.rtf','.nuspec','.json',''} and p.name!='RPL-1.5.txt')
    notices=[]
    for name in sorted(set(selected)):
        source=ROOT/name; target=destination/name
        if source.suffix.lower() in PRIVATE or not source.is_file() or source.is_symlink() or source.is_junction() or not source.resolve().is_relative_to(ROOT.resolve()):
            raise ValueError('Unsafe source item: '+name)
        target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(source,target)
        if name in changed and name in upstream and target.suffix.lower()!='.resw' and name!='LICENSE':
            if mark_change(target): notices.append(name)
    # Publish curated product/audit summaries, never private development logs,
    # test-machine paths, original API response bodies or historical screenshots.
    for name in PUBLIC_DOCS:
        source=ROOT/'docs/public-source'/name
        if not source.is_file() or source.is_symlink() or not source.resolve().is_relative_to(ROOT.resolve()):
            raise ValueError('Unsafe public document: '+name)
        shutil.copy2(source,destination/'docs'/name)
    # Keep the filename used by existing upgrade/E2E helpers, but only include
    # the four projects actually required for the LemonVN client/test build.
    solution=(ROOT/'GalgameManager.sln').read_text(encoding='utf-8-sig')
    removed=[]
    def keep_project(match):
        if match.group(1) not in PROJECTS:
            removed.append(match.group(2)); return ''
        return match.group(0)
    solution=re.sub(r'Project\([^\n]+?\) = "([^"]+)",[^\n]+?, "(\{[^}]+\})"\s*\nEndProject\s*\n',keep_project,solution)
    solution='\n'.join(line for line in solution.splitlines() if not any(guid in line for guid in removed))+'\n'
    lines=solution.splitlines(); at=next(i for i,l in enumerate(lines) if l.startswith('Microsoft Visual Studio Solution File'))+1
    lines.insert(at,'# LemonVN modification: client/core/WinUI-base and ALL client tests; inactive server projects remain in the development repository.')
    (destination/'GalgameManager.sln').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    (destination/'.gitignore').write_text('''**/bin/
**/obj/
.vs/
.idea/
.tools/
artifacts/
TestResults/
__pycache__/
*.py[cod]
*.pfx
*.p12
*.pem
*.key
*.snk
*.cer
*.crt
*.db
*.sqlite*
*.log
*.zip
*.7z
*.nupkg
*.dll
*.exe
*.pdb
''',encoding='utf-8')
    (destination/'.gitattributes').write_text('* text=auto\n*.png -text\n*.ico -text\n*.jpg -text\ndocs/licenses/** -text -whitespace\n',encoding='utf-8')
    (destination/'AGENTS.md').write_text('''# LemonVN source development

This is the independent WinUI3/MVVM/LiteDB client source snapshot based on PotatoVN.
Read README.md, NOTICE and docs/IMPLEMENTATION_LOG.md first. Keep offline Anime
features, Revision and EpisodeRecord semantics, ZIP V1/V2 and isolated data roots.
Do not re-enable legacy games, accounts, sync, plugins or upstream services.

Build unsigned x64: scripts/Build-Release.ps1 -RunTests (installed .NET8 SDK).
All GalgameManager.Test source files and anonymized TestData are preserved.
Phraser tests call unsupported legacy external game services; E2E needs a desktop.
Use separate synthetic data roots. Never use real collections for destructive tests.
Use file-scoped C# namespaces, four spaces, existing encodings/newlines and MVVM.
Edit/read .resw values only through GalgameManager/Strings/resw_tool.py; preserve
the ResX schema and use its validate/audit-snapshot commands.
No existing release/signing/upload workflow is included. No push, publication,
history rewriting, real credentials, signing key, account or license purchase
without explicit maintainer authorization. Preserve upstream attribution.
''',encoding='utf-8')
    normalized=(ROOT/'LICENSE').read_bytes().replace(b'\r\n',b'\n')
    provenance={'project':'https://github.com/38huangyukong38/Lemonvn','upstreamRepository':'https://github.com/GoldenPotato137/PotatoVN',
        'upstreamCommit':BASE,'upstreamLicenseNormalizedSha256':hashlib.sha256(normalized).hexdigest(),
        'oldGitHistoryCopied':False,'developmentRepositoryPreserved':True,'clientTestSourcesPreserved':True,
        'excludedComponents':['unused ASP.NET sync server and its separate tests','deprecated AuthServer','C++ game utility','plugin API test app','uninitialized PotatoVN.Doc submodule','old GitHub publishing workflows','internal logs/reports/reference screenshots and local tools/caches'],
        'modifiedInheritedFiles':sorted(set(notices)|{'GalgameManager.sln','GalgameManager/Strings/zh-CN/Resources.resw','GalgameManager/Strings/en-US/Resources.resw','GalgameManager/Strings/ja-JP/Resources.resw'})}
    (destination/'UPSTREAM_PROVENANCE.json').write_text(json.dumps(provenance,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    for source_or_target,label,mark in [(ROOT,'source',False),(destination,'snapshot',True)]:
        report=ROOT/('docs/verification/M6B2A-resources-'+label+'.json')
        args=[sys.executable,str(source_or_target/'GalgameManager/Strings/resw_tool.py'),'--dir',str(source_or_target/'GalgameManager/Strings'),'audit-snapshot','--report',str(report)]
        if mark: args.append('--mark-modified')
        subprocess.run(args,check=True,cwd=source_or_target)
    marker.unlink()
    test_sources=[p.relative_to(ROOT/'GalgameManager.Test').as_posix() for p in (ROOT/'GalgameManager.Test').rglob('*.cs') if not {'bin','obj'} & set(p.parts)]
    snapshot_tests=[p.relative_to(destination/'GalgameManager.Test').as_posix() for p in (destination/'GalgameManager.Test').rglob('*.cs')]
    if set(test_sources)!=set(snapshot_tests): raise ValueError('Client test source loss')
    report={'destination':str(destination),'sourceRoot':str(ROOT),'selectedFiles':len(set(selected))+5+len(PUBLIC_DOCS),
        'clientTestSourceFiles':len(test_sources),'allClientTestSourcesPreserved':True,'oldGitDirectoryCopied':False,
        'existingHistoryRewritten':False,'remoteAdded':False,'pushed':False,'excludedFiles':excluded,'modifiedInheritedFilesNoticed':notices}
    (ROOT/'docs/verification/M6B2A-snapshot-export.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k not in {'excludedFiles','modifiedInheritedFilesNoticed'}},ensure_ascii=False,indent=2))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--destination',type=Path,required=True)
    export(parser.parse_args().destination)
