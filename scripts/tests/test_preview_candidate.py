"""Synthetic approval-boundary checks; no real credentials or publication."""
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest

scripts = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(scripts))
spec=importlib.util.spec_from_file_location('preview_candidate',scripts/'prepare_preview_candidate.py')
preview=importlib.util.module_from_spec(spec)
spec.loader.exec_module(preview)


class PreviewApprovalBoundaryTests(unittest.TestCase):
    def test_missing_explicit_scope_is_rejected(self):
        with tempfile.TemporaryDirectory(prefix='LemonVN-ApprovalTest-') as temp:
            root=Path(temp)
            folder=root/'docs/release-review'
            folder.mkdir(parents=True)
            (folder/'MAINTAINER_CONFIRMATIONS.json').write_text(json.dumps({'confirmations':{}}))
            with self.assertRaisesRegex(ValueError,'scope confirmation'):
                preview.verify_maintainer_confirmations(root)

    def test_changed_substantive_terms_are_rejected_even_with_updated_file_hash(self):
        with tempfile.TemporaryDirectory(prefix='LemonVN-ApprovalTest-') as temp:
            root=Path(temp)
            folder=root/'docs/release-review'
            folder.mkdir(parents=True)
            original=folder/'2026-10-17-draft2-approved-source.md'
            current=root/'docs/SDK_END_USER_TERMS_DRAFT.md'
            original.write_text('## 1. Scope\nSynthetic original conditions.\n## 6. Choice\n')
            current.write_text('## 1. Scope\nSynthetic different conditions.\n## 6. Choice\n')
            fields=['windowsSdkPublisherResponsibilities','windowsAppSdkAndWindowsMlPublisherResponsibilities',
                    'newCodeCopyrightAttribution','suppliedIconPublicDistributionRights',
                    'draft2TermsAndExplicitFirstLaunchMechanism']
            record={'confirmations':{k:True for k in fields},'currentTermsSha256':preview.internal.sha(current),
                    'approvedDraft2Sha256':preview.internal.sha(original)}
            (folder/'MAINTAINER_CONFIRMATIONS.json').write_text(json.dumps(record))
            with self.assertRaisesRegex(ValueError,'Substantive component terms changed'):
                preview.verify_maintainer_confirmations(root)


if __name__=='__main__':
    unittest.main()
