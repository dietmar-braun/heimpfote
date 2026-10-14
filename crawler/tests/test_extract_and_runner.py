import unittest
from datetime import date

from tests.helpers import BASE, FakeFetcher, site
from tsc_crawler.extract import extract_animal, parse_html
from tsc_crawler.runner import crawl

TODAY = date(2026, 10, 13)
PAGES = {
    f"{BASE}/tiere/": "list.html",
    f"{BASE}/tiere/?seite=2": "list2.html",
    f"{BASE}/tiere/bello": "bello.html",
    f"{BASE}/tiere/luna": "luna.html",
    f"{BASE}/tiere/max": "max.html",
}


class ExtractTests(unittest.TestCase):
    def setUp(self):
        html = (__import__("pathlib").Path(__file__).parent / "fixtures" / "bello.html").read_text(encoding="utf-8")
        self.animal = extract_animal(parse_html(html), site(), f"{BASE}/tiere/bello", TODAY)

    def test_basic_fields(self):
        a = self.animal
        self.assertEqual(a.name, "Bello")
        self.assertEqual(a.external_id, "bello")        # falls back to the URL slug
        self.assertEqual(a.species, "Hund")
        self.assertEqual(a.sex, "Maennlich")
        self.assertEqual(a.size, "Gross")
        self.assertEqual(a.approx_age_months, 38)
        self.assertEqual(a.weight_kg, 28.5)
        self.assertEqual(a.adoption_fee, 1200.0)
        self.assertIs(a.is_neutered, True)
        self.assertTrue(a.is_mixed_breed)

    def test_compat_and_text(self):
        a = self.animal
        self.assertEqual(a.good_with_children, "Ja")
        self.assertEqual(a.good_with_cats, "Nein")
        self.assertEqual(a.good_with_dogs, "Unbekannt")
        self.assertEqual(a.description, "Freundlicher Rüde sucht aktive Familie.")

    def test_images_are_absolute_and_deduplicated(self):
        self.assertEqual(self.animal.image_urls,
                         [f"{BASE}/bilder/bello1.jpg", "https://cdn.example/b2.jpg"])

    def test_payload_is_camel_case_without_nulls(self):
        p = self.animal.to_payload()
        self.assertEqual(p["externalId"], "bello")
        self.assertEqual(p["approxAgeMonths"], 38)
        self.assertEqual(p["goodWithCats"], "Nein")
        self.assertNotIn("birthDate", p)
        self.assertNotIn("color", p)

    def test_missing_name_returns_none(self):
        self.assertIsNone(extract_animal(parse_html("<html><body></body></html>"), site(), BASE, TODAY))

    def test_species_falls_back_to_name_then_default(self):
        html = (__import__("pathlib").Path(__file__).parent / "fixtures" / "luna.html").read_text(encoding="utf-8")
        cfg = site()
        cfg.fields.pop("species")
        a = extract_animal(parse_html(html), cfg, f"{BASE}/tiere/luna", TODAY)
        self.assertEqual(a.species, "Katze")  # "Kätzchen Luna"
        cfg.default_species = "Hund"
        a = extract_animal(parse_html(html), cfg, f"{BASE}/tiere/luna", TODAY)
        self.assertEqual(a.species, "Hund")

    def test_explicit_external_id_with_regex(self):
        cfg = site()
        cfg.fields["external_id"] = __import__("tsc_crawler.config", fromlist=["FieldSpec"]).FieldSpec(
            selector=".nr", regex=r"Nr\.?\s*(\d+)")
        a = extract_animal(parse_html("<h1>X</h1><i class='nr'>Nr. 4711</i>"), cfg, f"{BASE}/x", TODAY)
        self.assertEqual(a.external_id, "4711")


class CrawlTests(unittest.TestCase):
    def test_follows_pagination_and_details_and_deduplicates(self):
        fetcher = FakeFetcher(PAGES)
        result = crawl(site(), fetcher, TODAY)
        self.assertEqual([a.external_id for a in result.animals], ["bello", "luna", "max"])
        self.assertEqual(result.errors, 0)
        self.assertEqual(result.pages, 2)
        self.assertTrue(result.is_complete_snapshot(site()))

    def test_failed_detail_page_prevents_snapshot(self):
        pages = dict(PAGES)
        pages[f"{BASE}/tiere/luna"] = None
        result = crawl(site(), FakeFetcher(pages), TODAY)
        self.assertEqual(len(result.animals), 2)
        self.assertEqual(result.errors, 1)
        self.assertFalse(result.is_complete_snapshot(site()))

    def test_failed_list_page_prevents_snapshot(self):
        pages = dict(PAGES)
        pages[f"{BASE}/tiere/?seite=2"] = None
        result = crawl(site(), FakeFetcher(pages), TODAY)
        self.assertEqual(result.errors, 1)
        self.assertFalse(result.is_complete_snapshot(site()))

    def test_max_pages_truncates(self):
        cfg = site(max_pages=1)
        result = crawl(cfg, FakeFetcher(PAGES), TODAY)
        self.assertTrue(result.truncated)
        self.assertFalse(result.is_complete_snapshot(cfg))

    def test_max_items_truncates(self):
        cfg = site(max_items=1)
        result = crawl(cfg, FakeFetcher(PAGES), TODAY)
        self.assertEqual(len(result.animals), 1)
        self.assertTrue(result.truncated)

    def test_layout_change_with_zero_animals_is_no_snapshot(self):
        cfg = site(item_selector="div.gibt-es-nicht")
        result = crawl(cfg, FakeFetcher(PAGES), TODAY)
        self.assertEqual(result.animals, [])
        self.assertFalse(result.is_complete_snapshot(cfg))

    def test_min_animals_threshold(self):
        cfg = site(min_animals=10)
        result = crawl(cfg, FakeFetcher(PAGES), TODAY)
        self.assertFalse(result.is_complete_snapshot(cfg))

    def test_pagination_loop_is_not_followed_twice(self):
        pages = {f"{BASE}/tiere/": "list.html", f"{BASE}/tiere/?seite=2": "list.html",
                 f"{BASE}/tiere/bello": "bello.html", f"{BASE}/tiere/luna": "luna.html"}
        fetcher = FakeFetcher(pages)
        crawl(site(), fetcher, TODAY)
        self.assertEqual(fetcher.requested.count(f"{BASE}/tiere/?seite=2"), 1)

    def test_list_mode_ids_come_from_item_link_or_name(self):
        cfg = site(start_urls=[f"{BASE}/l"], item_selector="li.t", detail_link_selector=None,
                   next_page_selector=None)
        fetcher = FakeFetcher({f"{BASE}/l": "list_mode.html"})
        result = crawl(cfg, fetcher, TODAY)
        self.assertEqual([a.name for a in result.animals], ["Rex", "Mia"])
        self.assertEqual([a.external_id for a in result.animals], ["rex-1", "mia"])
        self.assertEqual(result.animals[0].source_url, f"{BASE}/tiere/rex-1")
        self.assertEqual(result.animals[1].source_url, f"{BASE}/l")

if __name__ == "__main__":
    unittest.main()
