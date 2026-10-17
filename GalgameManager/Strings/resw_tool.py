#!/usr/bin/env python3
import argparse
import fnmatch
import hashlib
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from xml.sax.saxutils import escape


class ReswValidationError(Exception):
    """Raised when a .resw file fails structural validation."""


class ResourceManager:
    def __init__(self, base_dir="."):
        self.base_dir = Path(base_dir)
        self.language_files = self._find_resource_files()

    def _find_resource_files(self):
        """查找所有语言目录下的Resources.resw文件"""
        files = {}
        for item in self.base_dir.iterdir():
            if item.is_dir():
                resw_file = item / "Resources.resw"
                if resw_file.exists():
                    files[item.name] = resw_file
        return files

    def _parse_resw_file(self, file_path):
        """解析.resw文件，返回key-value字典"""
        try:
            tree = ET.parse(file_path)
            root = tree.getroot()
            data = {}

            for data_elem in root.findall(".//data"):
                name = data_elem.get("name")
                value_elem = data_elem.find("value")
                if name and value_elem is not None:
                    data[name] = value_elem.text or ""

            return data
        except ET.ParseError as e:
            print(f"解析文件 {file_path} 时出错: {e}")
            return {}

    @staticmethod
    def _read_text(file_path):
        raw = file_path.read_bytes()
        # ResW files in this repo are UTF-8 without BOM.
        text = raw.decode("utf-8")
        newline = "\r\n" if "\r\n" in text else "\n"
        return text, newline

    @staticmethod
    def _write_text(file_path, text):
        # Write exact bytes; do not let Python rewrite newlines.
        file_path.write_bytes(text.encode("utf-8"))

    @staticmethod
    def _escape_xml_text(value):
        # Match existing ResX style: escape &, <, > only; keep quotes raw in <value>.
        return escape(value)

    @staticmethod
    def _escape_xml_attr(value):
        return escape(value, {'"': "&quot;"})

    @staticmethod
    def _data_block_pattern(key):
        return re.compile(
            rf'(  <data name="{re.escape(key)}"[^>]*>\s*<value>)(.*?)(</value>\s*</data>)',
            re.DOTALL,
        )

    @staticmethod
    def validate_resw_file(file_path, require_key=None, require_value=None):
        """
        Validate a .resw file is well-formed ResX XML and not ElementTree-rewritten.

        Returns (ok: bool, errors: list[str]).
        """
        errors = []
        path = Path(file_path)

        try:
            text = path.read_bytes().decode("utf-8")
        except UnicodeDecodeError as e:
            return False, [f"不是合法 UTF-8: {e}"]

        if not text.lstrip().startswith("<?xml"):
            errors.append("缺少 XML 声明")
        else:
            first = text.lstrip().splitlines()[0]
            if "utf-8" not in first.lower():
                errors.append(f"XML 声明缺少 utf-8: {first!r}")
            # Visual Studio ResX uses double quotes; single quotes usually mean a bad rewrite.
            if "encoding='utf-8'" in first.lower():
                errors.append(f"XML 声明使用了单引号（疑似被重写）: {first!r}")

        # Detect the broken ElementTree rewrite shape from the old tool.
        if 'xmlns:ns1="urn:schemas-microsoft-com:xml-msdata"' in text:
            errors.append("检测到 ElementTree 重写痕迹 (xmlns:ns1)")
        if re.search(r"<root\b[^>]*\bxmlns:xs=", text):
            errors.append("检测到 ElementTree 重写痕迹 (xmlns:xs on <root>)")
        if "Microsoft ResX Schema" not in text:
            errors.append("缺少 Microsoft ResX Schema 注释")
        if "<xsd:schema" not in text:
            errors.append("缺少 <xsd:schema> 节点")
        if 'name="resmimetype"' not in text:
            errors.append("缺少 resheader resmimetype")
        if not text.rstrip().endswith("</root>"):
            errors.append("文件未以 </root> 结尾")

        try:
            root = ET.fromstring(text)
        except ET.ParseError as e:
            errors.append(f"XML 解析失败: {e}")
            return False, errors

        if root.tag != "root":
            errors.append(f"根节点应为 root，实际为 {root.tag!r}")

        data_elems = [e for e in root if e.tag == "data"]
        if not data_elems:
            errors.append("未找到任何 <data> 条目")

        names = []
        for elem in data_elems:
            name = elem.get("name")
            if not name:
                errors.append("存在缺少 name 属性的 <data>")
                continue
            names.append(name)
            if elem.find("value") is None:
                errors.append(f"<data name={name!r}> 缺少 <value>")

        dupes = sorted({n for n in names if names.count(n) > 1})
        if dupes:
            errors.append(f"存在重复 key: {', '.join(dupes[:5])}" + ("..." if len(dupes) > 5 else ""))

        if require_key is not None:
            matched = next((e for e in data_elems if e.get("name") == require_key), None)
            if matched is None:
                errors.append(f"编辑后未找到 key: {require_key!r}")
            elif require_value is not None:
                value_elem = matched.find("value")
                actual = "" if value_elem is None or value_elem.text is None else value_elem.text
                if actual != require_value:
                    errors.append(
                        f"key {require_key!r} 的值不符合预期: 期望 {require_value!r}, 实际 {actual!r}"
                    )

        return len(errors) == 0, errors

    def validate_all(self):
        """Validate every language Resources.resw. Returns {lang: (ok, errors)}."""
        results = {}
        for lang, file_path in self.language_files.items():
            results[lang] = self.validate_resw_file(file_path)
        return results

    def _write_text_validated(self, file_path, text, require_key=None, require_value=None):
        """
        Write text, validate, and roll back on failure.
        Returns True on success.
        """
        original = file_path.read_bytes()
        self._write_text(file_path, text)
        ok, errors = self.validate_resw_file(
            file_path, require_key=require_key, require_value=require_value
        )
        if ok:
            return True

        file_path.write_bytes(original)
        print(f"校验失败，已回滚 {file_path}:")
        for err in errors:
            print(f"  - {err}")
        return False

    @staticmethod
    def _strip_trailing_whitespace_text(text, newline):
        """
        Strip trailing whitespace, but preserve lines inside XML comments.

        ResX schema headers intentionally keep trailing spaces in the Microsoft
        comment block; stripping them creates noisy, unnecessary diffs.
        """
        had_final_newline = text.endswith("\n") or text.endswith("\r\n")
        in_comment = False
        lines = []
        for line in text.splitlines():
            if "<!--" in line:
                in_comment = True
            if in_comment:
                lines.append(line)
            else:
                lines.append(line.rstrip())
            if "-->" in line:
                in_comment = False
        normalized = newline.join(lines)
        if had_final_newline:
            normalized += newline
        return normalized

    def normalize_files(self):
        """清理所有资源文件的行尾空白"""
        updated_files = []
        for lang, file_path in self.language_files.items():
            text, newline = self._read_text(file_path)
            normalized = self._strip_trailing_whitespace_text(text, newline)
            if normalized == text:
                updated_files.append(lang)
                continue
            if self._write_text_validated(file_path, normalized):
                updated_files.append(lang)
            else:
                print(f"normalize 失败: {lang}")
        return updated_files

    def search_keys(self, pattern):
        """搜索匹配模式的keys，支持通配符"""
        results = {}

        for lang, file_path in self.language_files.items():
            data = self._parse_resw_file(file_path)
            matching_keys = [key for key in data.keys() if fnmatch.fnmatch(key, pattern)]

            for key in matching_keys:
                if key not in results:
                    results[key] = {}
                results[key][lang] = data[key]

        return results

    def update_key(self, key, translations):
        """更新指定key在各语言文件中的值"""
        updated_files = []

        for lang, value in translations.items():
            if lang not in self.language_files:
                print(f"警告: 语言 {lang} 的资源文件不存在")
                continue

            file_path = self.language_files[lang]
            if self._update_resw_file(file_path, key, value):
                updated_files.append(f"{lang}")

        return updated_files

    def delete_key(self, key):
        """从所有语言资源文件中删除指定key"""
        updated_files = []
        for lang, file_path in self.language_files.items():
            try:
                text, _newline = self._read_text(file_path)
                pattern = self._data_block_pattern(key)
                new_text, count = pattern.subn("", text, count=1)
                if count == 0:
                    continue
                # Avoid leaving a blank line gap when possible.
                new_text = re.sub(r"\n{3,}", "\n\n", new_text)
                new_text = re.sub(r"\n+</root>\s*$", "\n</root>\n", new_text)
                if not self._write_text_validated(file_path, new_text):
                    print(f"删除后校验失败: {lang}")
                    continue
                if key in self._parse_resw_file(file_path):
                    print(f"删除后仍能读到 key {key!r} ({lang})，视为失败")
                    continue
                updated_files.append(lang)
            except Exception as e:
                print(f"删除文件 {file_path} 中的key时出错: {e}")
        return updated_files

    def _update_resw_file(self, file_path, key, value):
        """更新单个.resw文件中的key-value，保留原有 XML 结构与格式"""
        try:
            text, newline = self._read_text(file_path)
            escaped_value = self._escape_xml_text(value)
            pattern = self._data_block_pattern(key)

            if pattern.search(text):
                text = pattern.sub(rf"\g<1>{escaped_value}\g<3>", text, count=1)
            else:
                block = (
                    f'  <data name="{self._escape_xml_attr(key)}" xml:space="preserve">{newline}'
                    f"    <value>{escaped_value}</value>{newline}"
                    f"  </data>{newline}"
                )
                root_idx = text.rfind("</root>")
                if root_idx == -1:
                    print(f"更新文件 {file_path} 时出错: 未找到 </root>")
                    return False
                text = text[:root_idx] + block + text[root_idx:]

            return self._write_text_validated(
                file_path, text, require_key=key, require_value=value
            )

        except Exception as e:
            print(f"更新文件 {file_path} 时出错: {e}")
            return False


def audit_snapshot_resources(manager, report_path, mark_modified=False):
    """Redacted resource audit and value-preserving Apache modification notice."""
    patterns = {
        "github-token-shape": re.compile(r"(?:gh[pousr]_[A-Za-z0-9]{36}|github_pat_[A-Za-z0-9_]{50,})"),
        "private-key-header": re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----"),
        "literal-credential": re.compile(r'''(?i)["'](?:access_token|client_secret|password|api_key)["']\s*:\s*["'][A-Za-z0-9_+/.=-]{16,}["']'''),
    }
    rows, findings = [], []
    for language, path in manager.language_files.items():
        values = manager._parse_resw_file(path)
        text, newline = manager._read_text(path)
        original = text
        for key, value in values.items():
            for rule, pattern in patterns.items():
                if pattern.search(value):
                    findings.append({"language": language, "key": key, "rule": rule})
        if mark_modified:
            marker = "<!-- LemonVN modification: independent application identity and Anime/recognition UI resources; upstream copyright retained in LICENSE/NOTICE. -->"
            if marker not in text:
                offset = text.index("?>") + 2 if text.startswith("<?xml") else 0
                text = text[:offset] + newline + marker + text[offset:]
                manager._write_text(path, text)
            valid, errors = manager.validate_resw_file(path)
            if not valid or manager._parse_resw_file(path) != values:
                manager._write_text(path, original)
                raise ReswValidationError("Snapshot notice changed resource values or structure: " + language)
        rows.append({"language": language, "entries": len(values), "valuesPreserved": True,
                     "modifiedNotice": mark_modified, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    result = {"resources": rows, "findings": findings, "valuesRecorded": False}
    Path(report_path).write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return not findings


def main():
    parser = argparse.ArgumentParser(description="Windows资源文件管理工具")
    parser.add_argument("--dir", default=".", help="资源文件根目录 (默认: 当前目录)")

    subparsers = parser.add_subparsers(dest="command", help="可用命令")

    # 搜索命令
    search_parser = subparsers.add_parser("search", help="搜索资源key")
    search_parser.add_argument("pattern", help="搜索模式 (支持通配符，如: *Login*)")

    # 更新命令
    update_parser = subparsers.add_parser("update", help="更新资源key")
    update_parser.add_argument("key", help="要更新的key")
    update_parser.add_argument(
        "translations",
        nargs="+",
        help="语言-值对，格式: lang1=value1 lang2=value2",
    )

    delete_parser = subparsers.add_parser("delete", help="删除资源key")
    delete_parser.add_argument("key", help="要删除的key")
    subparsers.add_parser("normalize", help="清理资源文件行尾空白")
    subparsers.add_parser("validate", help="校验所有资源文件是否为合法 ResX")
    audit_parser = subparsers.add_parser("audit-snapshot", help="仅输出脱敏检查，可标记快照修改说明而不改变资源值")
    audit_parser.add_argument("--report", required=True)
    audit_parser.add_argument("--mark-modified", action="store_true")

    args = parser.parse_args()

    if not args.command:
        parser.print_help()
        return

    rm = ResourceManager(args.dir)

    if not rm.language_files:
        print("未找到任何语言资源文件")
        return

    print(f"找到语言文件: {', '.join(rm.language_files.keys())}")

    if args.command == "audit-snapshot":
        if not audit_snapshot_resources(rm, args.report, args.mark_modified):
            sys.exit(1)
        print("Resource audit passed; values were not printed or changed.")
    elif args.command == "search":
        results = rm.search_keys(args.pattern)

        if not results:
            print(f"未找到匹配模式 '{args.pattern}' 的key")
            return

        print(f"\n匹配模式 '{args.pattern}' 的结果:")
        print("-" * 80)

        for key, translations in results.items():
            print(f"\nKey: {key}")
            for lang, value in translations.items():
                print(f"  {lang}: {value}")

    elif args.command == "update":
        # 解析语言-值对
        translations = {}
        try:
            for item in args.translations:
                if "=" not in item:
                    print(f"错误: 格式不正确 '{item}'，应为 language=value")
                    return

                lang, value = item.split("=", 1)
                translations[lang] = value
        except ValueError:
            print("错误: 语言-值对格式不正确")
            return

        updated_files = rm.update_key(args.key, translations)

        if updated_files:
            print(f"成功更新key '{args.key}' 在以下语言中: {', '.join(updated_files)}")
        else:
            print("未能更新任何文件")
            sys.exit(1)
    elif args.command == "delete":
        updated_files = rm.delete_key(args.key)
        if updated_files:
            print(f"成功删除key '{args.key}'，涉及语言: {', '.join(updated_files)}")
        else:
            print(f"未找到key '{args.key}'")
            sys.exit(1)
    elif args.command == "normalize":
        updated_files = rm.normalize_files()
        print(f"成功清理资源文件: {', '.join(updated_files)}")
    elif args.command == "validate":
        results = rm.validate_all()
        failed = False
        for lang, (ok, errors) in results.items():
            if ok:
                print(f"[OK] {lang}")
            else:
                failed = True
                print(f"[FAIL] {lang}")
                for err in errors:
                    print(f"  - {err}")
        if failed:
            sys.exit(1)
        print("全部资源文件校验通过")


if __name__ == "__main__":
    main()
