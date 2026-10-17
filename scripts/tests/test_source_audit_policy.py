"""Initial snapshots and subsequent public maintenance have distinct history rules."""
import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import audit_source_snapshot as audit


class SourceHistoryPolicyTests(unittest.TestCase):
    def history(self):
        return dict(shallow=False, alternatesPresent=False, upstreamPfxObjectPresent=False,
                    oneRootCommit=False, roots=[audit.PUBLIC_ROOT], remotes=['origin'],
                    remoteUrls=[audit.PUBLIC_REMOTE])

    def test_maintenance_accepts_only_reviewed_independent_history(self):
        history = self.history()
        self.assertTrue(audit.history_allowed(history, maintenance=True))
        self.assertFalse(audit.history_allowed(history))
        for key, value in [('roots', ['unreviewed-root']), ('remoteUrls', ['https://example.invalid/repo']),
                           ('upstreamPfxObjectPresent', True), ('alternatesPresent', True), ('shallow', True)]:
            with self.subTest(key=key):
                self.assertFalse(audit.history_allowed({**history, key: value}, maintenance=True))

    def test_initial_snapshot_still_requires_single_commit_and_no_remote(self):
        history = {**self.history(), 'oneRootCommit': True, 'remotes': [], 'remoteUrls': []}
        self.assertTrue(audit.history_allowed(history))
        self.assertFalse(audit.history_allowed({**history, 'remotes': ['origin']}))


if __name__ == '__main__':
    unittest.main()
