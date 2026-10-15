import unittest

from tests.helpers import FIXTURES, FakeFetcher
from tsc_crawler.config import parse_site
from tsc_crawler.extract import parse_html
from tsc_crawler.runner import crawl
from tsc_crawler.suggest import (SuggestError, candidate_links, detect_detail_link, detect_items,
                                 detect_label_fields, detect_next, sitemap_candidates, suggest)

B = "https://tierheim.test"
PAGES = {
    f"{B}": "s_home.html",
    f"{B}/tiere/hunde/": "s_list1.html",
    f"{B}/tiere/hunde/seite/2/": "s_list2.html",
    **{f"{B}/tiere/{s}/": f"s_detail_{s}.html" for s in ("bello", "rex", "luna", "a", "b", "c", "d")},
}


def read(name):
    return (FIXTURES / name).read_text(encoding="utf-8")


class DiscoveryTests(unittest.TestCase):
    def test_candidate_links_rank_animal_pages_and_skip_impressum(self):
        links = candidate_links(parse_html(read("s_home.html")), B)
        urls = [u for _, u in links]
        self.assertIn(f"{B}/tiere/", urls)
        self.assertIn(f"{B}/tiere/hunde/", urls)
        self.assertNotIn(f"{B}/impressum/", urls)
        self.assertNotIn(f"{B}/spenden/", urls)

    def test_sitemap_candidates_keep_shallow_relevant_urls(self):
        fetcher = type("F", (), {"get_raw": lambda self, u: read("s_sitemap.xml") if u.endswith("sitemap.xml") else None})()
        urls = [u for _, u in sitemap_candidates(fetcher, B)]
        self.assertEqual(urls, [f"{B}/tiervermittlung/"])


class ItemDetectionTests(unittest.TestCase):
    def setUp(self):
        self.soup = parse_html(read("s_list1.html"))

    def test_finds_cards_not_menu_or_sidebar(self):
        group = detect_items(self.soup)
        self.assertEqual(len(group.elements), 3)
        self.assertEqual(group.selector, "div.tier-card")   # 'post-123' is ignored as dynamic
        self.assertTrue(group.exact)

    def test_detail_link_is_the_heading_link(self):
        group = detect_items(self.soup)
        self.assertEqual(detect_detail_link(group.elements), "h3 a[href]")

    def test_next_page(self):
        self.assertEqual(detect_next(self.soup), "a.next")

    def test_no_items_on_page_without_cards(self):
        self.assertIsNone(detect_items(parse_html(read("s_home.html"))))


class LabelTests(unittest.TestCase):
    def test_definition_list(self):
        scope = parse_html(read("s_detail_bello.html")).select_one("main")
        fields = detect_label_fields(scope)
        self.assertEqual(fields["age"]["selector"], 'dt:-soup-contains-own("Alter") + dd')
        self.assertEqual(set(fields) & {"age", "sex", "breed", "size", "cats", "children"},
                         {"age", "sex", "breed", "size", "cats", "children"})

    def test_inline_and_table_variants_extract_correct_values(self):
        html = read("s_inline_detail.html")
        soup = parse_html(html)
        fields = detect_label_fields(soup.select_one("main"))
        self.assertEqual(set(fields), {"age", "sex", "breed", "weight", "fee"})
        cfg = parse_site({"name": "x", "start_urls": ["http://x"], "item_selector": "main",
                          "fields": {"name": "h1", **{k: v for k, v in fields.items()}}})
        from tsc_crawler.extract import extract_animal
        a = extract_animal(soup, cfg, "http://x/mia")
        self.assertEqual(a.approx_age_months, 24)
        self.assertEqual(a.sex, "Weiblich")
        self.assertEqual(a.breed_name, "Europäisch Kurzhaar")
        self.assertEqual(a.weight_kg, 4.5)
        self.assertEqual(a.adoption_fee, 80.0)


class SuggestTests(unittest.TestCase):
    def test_full_suggestion_produces_a_working_config(self):
        result = suggest(B, FakeFetcher(PAGES))
        cfg = result.config
        self.assertEqual(cfg["name"], "Tierheim Musterstadt")   # from the home page title
        self.assertEqual(cfg["item_selector"], "div.tier-card")
        self.assertEqual(cfg["detail_link_selector"], "h3 a[href]")
        self.assertEqual(cfg["next_page_selector"], "a.next")
        self.assertIn(f"{B}/tiere/hunde/", cfg["start_urls"])
        self.assertEqual(cfg["default_species"], "Hund")
        self.assertEqual(cfg["fields"]["name"], "h1")
        self.assertEqual(cfg["fields"]["images"]["selector"], "main img")
        self.assertIn("description", cfg["fields"])

        # the YAML draft must load and crawl correctly
        yaml_text = result.to_yaml()
        self.assertIn("REVIEW BEFORE USE", yaml_text)
        import yaml
        loaded = parse_site(yaml.safe_load(yaml_text))
        crawled = crawl(loaded, FakeFetcher(PAGES))
        self.assertEqual(len(crawled.animals), 6)
        bello = crawled.animals[0]
        self.assertEqual((bello.name, bello.species, bello.sex, bello.size), ("Bello", "Hund", "Maennlich", "Gross"))
        self.assertEqual(bello.approx_age_months, 36)
        self.assertEqual(bello.good_with_cats, "Nein")
        self.assertEqual(len(bello.image_urls), 2)
        self.assertNotIn("logo", " ".join(bello.image_urls))

    def test_verification_report(self):
        result = suggest(B, FakeFetcher(PAGES))
        self.assertTrue(result.report[0].startswith("Test crawl: 3 animal(s)"))
        self.assertTrue(any("images" in line and "3/3" in line for line in result.report))

    def test_explicit_list_url_skips_discovery(self):
        fetcher = FakeFetcher(PAGES)
        suggest(B, fetcher, list_url=f"{B}/tiere/hunde/", verify=False)
        self.assertNotIn(B, fetcher.requested)

    def test_no_list_found_raises(self):
        with self.assertRaises(SuggestError):
            suggest(B, FakeFetcher({B: "s_home.html"}))

    def test_unreachable_site_raises(self):
        with self.assertRaises(SuggestError):
            suggest(B, FakeFetcher({}))


if __name__ == "__main__":
    unittest.main()
