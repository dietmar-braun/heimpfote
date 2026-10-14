import unittest

from tsc_crawler.client import ImportClient, ImportFailed, MAX_BATCH_SIZE
from tsc_crawler.config import load_site, parse_site
from tsc_crawler.detect import detect_cms
from tsc_crawler.models import Animal
from tests.helpers import FIXTURES


class FakeResponse:
    def __init__(self, status=200, body=None):
        self.status_code = status
        self._body = body if body is not None else {"created": 1, "updated": 2, "unchanged": 3, "archived": 4}
        self.text = str(self._body)

    def json(self):
        return self._body


class FakeSession:
    def __init__(self, responses=None):
        self.headers = {}
        self.calls = []
        self.responses = list(responses or [])

    def post(self, url, json, timeout):
        self.calls.append((url, json))
        return self.responses.pop(0) if self.responses else FakeResponse()


def animals(count):
    return [Animal(external_id=f"a{i}", name=f"Tier {i}") for i in range(count)]


class ClientTests(unittest.TestCase):
    def test_sends_api_key_and_payload(self):
        session = FakeSession()
        client = ImportClient("http://localhost:5080/", "tsc_key", session)
        summary = client.push(animals(2), is_full_snapshot=True)
        url, body = session.calls[0]
        self.assertEqual(url, "http://localhost:5080/api/import/animals")
        self.assertEqual(session.headers["X-Api-Key"], "tsc_key")
        self.assertEqual(body["source"], "Crawler")
        self.assertTrue(body["isFullSnapshot"])
        self.assertEqual(body["animals"][0]["externalId"], "a0")
        self.assertEqual((summary.created, summary.updated, summary.unchanged, summary.archived), (1, 2, 3, 4))

    def test_empty_list_sends_nothing(self):
        session = FakeSession()
        ImportClient("http://x", "k", session).push([], True)
        self.assertEqual(session.calls, [])

    def test_error_response_raises(self):
        session = FakeSession([FakeResponse(401, {"error": "nope"})])
        with self.assertRaises(ImportFailed):
            ImportClient("http://x", "k", session).push(animals(1), False)

    def test_large_snapshot_is_downgraded_and_batched(self):
        session = FakeSession()
        summary = ImportClient("http://x", "k", session).push(animals(MAX_BATCH_SIZE + 1), True)
        self.assertEqual(len(session.calls), 2)
        self.assertTrue(all(call[1]["isFullSnapshot"] is False for call in session.calls))
        self.assertEqual(summary.created, 2)


class DetectTests(unittest.TestCase):
    def test_generator_meta_with_version(self):
        html = '<html><head><meta name="generator" content="WordPress 6.6.1"></head></html>'
        self.assertEqual(detect_cms(html), ("WordPress", "6.6.1"))

    def test_wordpress_by_path(self):
        self.assertEqual(detect_cms('<link href="/wp-content/themes/x/style.css">')[0], "WordPress")

    def test_typo3_and_joomla(self):
        self.assertEqual(detect_cms('<script src="/typo3conf/ext/x.js"></script>')[0], "TYPO3")
        self.assertEqual(detect_cms('<meta name="generator" content="Joomla! - Open Source">')[0], "Joomla")

    def test_unknown(self):
        self.assertEqual(detect_cms("<html><body>hi</body></html>"), ("Unbekannt", None))


class ConfigTests(unittest.TestCase):
    def test_example_config_loads(self):
        cfg = load_site(FIXTURES.parent.parent / "config" / "example-site.yaml")
        self.assertEqual(cfg.name, "Beispiel Tierheim")
        self.assertEqual(cfg.fields["images"].attr, "src")
        self.assertEqual(cfg.fields["name"].selector, "h1")  # shorthand string form

    def test_missing_required_key(self):
        with self.assertRaises(ValueError):
            parse_site({"name": "x", "start_urls": ["http://x"], "fields": {"name": "h1"}})

    def test_unknown_field_rejected(self):
        with self.assertRaises(ValueError):
            parse_site({"name": "x", "start_urls": ["http://x"], "item_selector": "li",
                        "fields": {"name": "h1", "foo": "p"}})

    def test_name_field_required(self):
        with self.assertRaises(ValueError):
            parse_site({"name": "x", "start_urls": ["http://x"], "item_selector": "li",
                        "fields": {"species": "p"}})

    def test_single_start_url_string(self):
        cfg = parse_site({"name": "x", "start_urls": "http://x", "item_selector": "li",
                          "fields": {"name": "h1"}})
        self.assertEqual(cfg.start_urls, ["http://x"])


if __name__ == "__main__":
    unittest.main()
