import tempfile
import unittest
from pathlib import Path
from unittest import mock

import yaml

from tests.helpers import FakeFetcher
from tests.test_suggest import PAGES
from tsc_crawler import cli


class CliTests(unittest.TestCase):
    """The CLI module must import and its sub-commands must work end to end (with a fake fetcher)."""

    def _patched(self):
        return mock.patch.object(cli, "HttpFetcher", lambda **kw: FakeFetcher(PAGES))

    def test_suggest_writes_a_loadable_yaml_file(self):
        with tempfile.TemporaryDirectory() as tmp, self._patched():
            out = Path(tmp) / "site.yaml"
            self.assertEqual(cli.main(["suggest", "https://tierheim.test", "--out", str(out)]), 0)
            data = yaml.safe_load(out.read_text(encoding="utf-8"))
        self.assertEqual(data["item_selector"], "div.tier-card")

    def test_suggest_without_list_page_fails_cleanly(self):
        with self._patched():
            self.assertEqual(cli.main(["suggest", "https://other.test"]), 1)

    def test_run_dry_run_prints_payload(self):
        cfg = {"name": "T", "start_urls": ["https://tierheim.test/tiere/hunde/"], "item_selector": "div.tier-card",
               "detail_link_selector": "h3 a[href]", "delay_seconds": 0, "fields": {"name": "h1"}}
        with tempfile.TemporaryDirectory() as tmp, self._patched():
            path = Path(tmp) / "t.yaml"
            path.write_text(yaml.safe_dump(cfg), encoding="utf-8")
            with mock.patch("builtins.print") as printed:
                self.assertEqual(cli.main(["run", str(path), "--dry-run"]), 0)
        self.assertIn("Bello", printed.call_args[0][0])


if __name__ == "__main__":
    unittest.main()
