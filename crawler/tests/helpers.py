from pathlib import Path

from tsc_crawler.config import parse_site

FIXTURES = Path(__file__).parent / "fixtures"
BASE = "https://tierheim.test"


class FakeFetcher:
    """Serves fixture files for URLs; unknown URLs behave like failed requests."""

    def __init__(self, pages: dict[str, str | None]):
        self.pages = pages
        self.requested: list[str] = []

    def get(self, url):
        self.requested.append(url)
        name = self.pages.get(url)
        if name is None:
            return None
        return (FIXTURES / name).read_text(encoding="utf-8")


def site(**overrides):
    raw = {
        "name": "Test",
        "start_urls": [f"{BASE}/tiere/"],
        "item_selector": "article.tier",
        "detail_link_selector": "a.mehr",
        "next_page_selector": "a.next",
        "min_animals": 1,
        "fields": {
            "name": "h1", "species": ".art", "breed": ".rasse", "sex": ".geschlecht", "age": ".alter",
            "size": ".groesse", "weight": ".gewicht", "fee": ".schutzgebuehr", "neutered": ".kastriert",
            "children": ".kinder", "cats": ".katzen", "description": ".beschreibung",
            "images": {"selector": ".galerie img", "attr": "src"},
        },
    }
    raw.update(overrides)
    return parse_site(raw)
