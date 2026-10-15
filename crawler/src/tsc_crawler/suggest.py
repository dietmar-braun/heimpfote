"""Semi-automatic onboarding: proposes a site configuration for a shelter website.

Steps: find the page that lists the animals -> detect the repeating animal elements ->
read one detail page to map labels like "Alter:" to fields -> verify with a small test crawl.
The result is a YAML draft that a human reviews (`--dry-run`) before it goes live.
"""

from __future__ import annotations

import logging
import re
from dataclasses import dataclass, field
from urllib.parse import urljoin, urlsplit

import yaml
from bs4 import BeautifulSoup
from bs4.element import NavigableString, Tag

from .config import parse_site
from .extract import parse_html
from .fetcher import Fetcher
from .normalize import normalize_species
from .runner import crawl

log = logging.getLogger(__name__)


class SuggestError(RuntimeError):
    pass


class CachingFetcher:
    """Avoids fetching the same page twice during discovery."""

    def __init__(self, inner: Fetcher):
        self.inner = inner
        self.cache: dict[str, str | None] = {}

    def get(self, url: str) -> str | None:
        if url not in self.cache:
            self.cache[url] = self.inner.get(url)
        return self.cache[url]

    def get_raw(self, url: str) -> str | None:
        """Any content type (sitemap.xml); falls back to get() for fetchers without get_raw."""
        key = f"raw:{url}"
        if key not in self.cache:
            raw = getattr(self.inner, "get_raw", self.inner.get)
            self.cache[key] = raw(url)
        return self.cache[key]


# ---------------------------------------------------------------- CSS helpers

_IGNORED_CLASS = re.compile(
    r"^(active|current|first|last|odd|even|selected|open|show|hidden|clearfix|animated|"
    r"(col|is|has|js|wp|post|type|status|category|tag|elementor-element)-.*|.*\d{3,}.*)$")
_VALID_CLASS = re.compile(r"^[A-Za-z_][\w-]*$")
_STRUCTURAL = ("header", "footer", "nav", "aside")


def _classes(el: Tag) -> list[str]:
    return [c for c in (el.get("class") or []) if _VALID_CLASS.match(c) and not _IGNORED_CLASS.match(c)]


def css_for(el: Tag, max_classes: int = 2) -> str:
    if el.get("id") and _VALID_CLASS.match(el["id"]) and not re.search(r"\d{3,}", el["id"]):
        return f"#{el['id']}"
    return el.name + "".join(f".{c}" for c in _classes(el)[:max_classes])


def _real_href(el: Tag | None) -> str | None:
    href = el.get("href") if el else None
    if not href or href.startswith(("#", "javascript:", "mailto:", "tel:")):
        return None
    return href


def _in_structure(el: Tag) -> bool:
    return el.name in _STRUCTURAL or el.find_parent(_STRUCTURAL) is not None


# ------------------------------------------------------------ list page links

_POSITIVE = [(r"tiervermittlung|vermittlung", 5),
             (r"unsere-tiere|tiere-suchen|zuhause|zu-hause|adoption|adoptier|tiere suchen", 4),
             (r"hunde|katzen|kleintiere|kaninchen", 3),
             (r"tiere|tier", 2)]
_NEGATIVE = re.compile(
    r"impressum|datenschutz|spende|kontakt|ueber-uns|über uns|about|news|aktuell|veranstalt|shop|login|"
    r"anmeld|agb|presse|jobs|stellen|patenschaft|verloren|fundtier|vermisst|nothilfe|sachspende|mitglied|"
    r"archiv|vermittelt|gluecks|glücks|wunschliste|ratgeber|tipps", re.I)
_SKIP_EXT = re.compile(r"\.(pdf|jpe?g|png|gif|webp|zip|docx?|xlsx?)$", re.I)


def _same_site(a: str, b: str) -> bool:
    strip = lambda u: urlsplit(u).netloc.lower().removeprefix("www.")  # noqa: E731
    return strip(a) == strip(b)


def _score_link(url: str, text: str) -> int:
    haystack = f"{urlsplit(url).path} {text}".lower()
    score = sum(weight for pattern, weight in _POSITIVE if re.search(pattern, haystack))
    if _NEGATIVE.search(haystack):
        score -= 6
    return score


def candidate_links(soup: BeautifulSoup, base_url: str) -> list[tuple[int, str]]:
    found: dict[str, int] = {}
    for a in soup.find_all("a"):
        href = _real_href(a)
        if not href:
            continue
        url = urljoin(base_url, href).split("#")[0]
        if not url.startswith("http") or not _same_site(url, base_url) or _SKIP_EXT.search(url):
            continue
        score = _score_link(url, a.get_text(" ", strip=True))
        if score > 0:
            found[url] = max(found.get(url, 0), score)
    return sorted(((s, u) for u, s in found.items()), key=lambda x: (-x[0], len(x[1])))


def sitemap_candidates(fetcher, origin: str) -> list[tuple[int, str]]:
    """Reads sitemap.xml (one level of sub-sitemaps) and keeps shallow, animal-related URLs."""
    locs: list[str] = []
    top = fetcher.get_raw(f"{origin}/sitemap.xml") or fetcher.get_raw(f"{origin}/wp-sitemap.xml")
    if not top:
        return []
    children = re.findall(r"<loc>\s*([^<\s]+)\s*</loc>", top)
    for url in children:
        if url.lower().endswith(".xml"):
            if len(locs) < 3000 and (sub := fetcher.get_raw(url)):
                locs += re.findall(r"<loc>\s*([^<\s]+)\s*</loc>", sub)
        else:
            locs.append(url)
    result = []
    for url in locs[:3000]:
        depth = len([p for p in urlsplit(url).path.split("/") if p])
        if depth == 0 or depth > 3 or _SKIP_EXT.search(url):
            continue
        score = _score_link(url, "") - 1 + (1 if depth == 1 else 0)
        if score > 0:
            result.append((score, url))
    return sorted(result, key=lambda x: (-x[0], len(x[1])))[:10]


# ------------------------------------------------------ repeating animal items

@dataclass
class ItemGroup:
    selector: str
    elements: list[Tag]
    score: float
    exact: bool = True


def detect_items(soup: BeautifulSoup) -> ItemGroup | None:
    """Finds the largest group of similar sibling elements that looks like a list of animal cards."""
    best: ItemGroup | None = None
    for parent in soup.find_all(True):
        if parent.name in ("html", "head", "script", "style", "select", "nav") or _in_structure(parent):
            continue
        groups: dict[tuple, list[Tag]] = {}
        for child in parent.find_all(True, recursive=False):
            if child.name in ("script", "style", "br", "hr"):
                continue
            groups.setdefault((child.name, tuple(_classes(child))), []).append(child)
        for elements in groups.values():
            group = _score_group(soup, parent, elements)
            if group and (best is None or group.score > best.score):
                best = group
    return best


def _score_group(soup: BeautifulSoup, parent: Tag, elements: list[Tag]) -> ItemGroup | None:
    n = len(elements)
    if n < 3:
        return None
    texts = [e.get_text(" ", strip=True) for e in elements]
    if len(set(texts)) < 0.7 * n:
        return None
    link_frac = sum(1 for e in elements if _real_href(e) or _real_href(e.find("a", href=True))) / n
    if link_frac < 0.6:
        return None
    img_frac = sum(1 for e in elements if e.find("img")) / n
    head_frac = sum(1 for e in elements if e.find(re.compile(r"^h[1-6]$"))) / n
    avg_len = sum(len(t) for t in texts) / n
    if avg_len < 4 or avg_len > 900:
        return None
    if not (img_frac >= 0.3 or head_frac >= 0.5 or avg_len >= 25):
        return None
    score = min(n, 60) * (1 + img_frac + head_frac) * (1.0 if 10 <= avg_len <= 500 else 0.5)
    selector, exact = _selector_for_group(soup, parent, elements)
    return ItemGroup(selector, elements, score, exact)


def _selector_for_group(soup: BeautifulSoup, parent: Tag, elements: list[Tag]) -> tuple[str, bool]:
    own = elements[0].name + "".join(f".{c}" for c in _classes(elements[0])[:2])
    wanted = [id(e) for e in elements]
    candidates = [own, f"{css_for(parent)} > {own}"]
    for candidate in candidates:
        if [id(m) for m in soup.select(candidate)] == wanted:
            return candidate, True
    return candidates[1], False


def detect_next(soup: BeautifulSoup) -> str | None:
    if soup.select_one('a[rel~="next"][href]'):
        return 'a[rel~="next"]'
    for a in soup.find_all("a"):
        if _real_href(a) and any("next" in c.lower() for c in (a.get("class") or [])) and _VALID_CLASS.match(
                next((c for c in a.get("class") if "next" in c.lower()), "")):
            return "a." + next(c for c in a.get("class") if "next" in c.lower())
    labels = {"weiter", "nächste", "nächste seite", "next", "»", "›", ">", "vor"}
    for a in soup.find_all("a"):
        text = a.get_text(" ", strip=True)
        if _real_href(a) and text.lower() in labels and "\"" not in text:
            return f'a:-soup-contains-own("{text}")'
    return None


def detect_detail_link(items: list[Tag]) -> str | None:
    if all(i.name == "a" and _real_href(i) for i in items):
        return "a"
    for tag in ("h1", "h2", "h3", "h4", "h5", "h6"):
        if all(_real_href(i.select_one(f"{tag} a[href]")) for i in items):
            return f"{tag} a[href]"
    firsts = [i.find("a", href=True) for i in items]
    hrefs = {_real_href(a) for a in firsts if a}
    if all(_real_href(a) for a in firsts) and len(hrefs) >= 0.8 * len(items):
        return "a[href]"
    return None


# ------------------------------------------------------------ detail page fields

_LABELS: dict[str, list[str]] = {
    "age": ["alter", "geboren", "geburtsdatum", "geb."],
    "sex": ["geschlecht"],
    "breed": ["rasse"],
    "species": ["tierart", "art"],
    "size": ["größe", "groesse", "schulterhöhe"],
    "weight": ["gewicht"],
    "color": ["farbe", "fellfarbe"],
    "neutered": ["kastriert", "kastration", "sterilisiert"],
    "fee": ["schutzgebühr", "schutzgebuehr", "vermittlungsgebühr", "gebühr"],
    "children": ["kinder", "kinderverträglich"],
    "dogs": ["hunde", "hundeverträglich"],
    "cats": ["katzen", "katzenverträglich"],
    "apartment": ["wohnungshaltung", "wohnung"],
}
_COMPAT_KEYS = {"children", "dogs", "cats"}
_NEXT_LABEL = r"(?=\s+[A-ZÄÖÜ][\wäöüß/]+:|$)"


def _label_key(text: str) -> str | None:
    t = re.sub(r"[:\s]+$", "", text.strip().lower())
    if not t or len(t) > 45:
        return None
    for key, words in _LABELS.items():
        for w in words:
            if t == w or (len(w) > 5 and t.startswith(w)) or (
                    key in _COMPAT_KEYS and "verträglich" in t and w.removesuffix("verträglich") in t):
                return key
    return None


def _next_content(el: Tag) -> Tag | NavigableString | None:
    for sib in el.next_siblings:
        if isinstance(sib, Tag):
            return sib if sib.get_text(strip=True) else None
        if str(sib).strip():
            return sib
    return None


def detect_label_fields(scope: Tag) -> dict[str, dict]:
    """Maps German labels ("Alter", "Rasse: Labrador") to field specs. First occurrence wins."""
    fields: dict[str, dict] = {}
    for el in scope.find_all(["dt", "th", "td", "strong", "b", "span", "label", "div", "p", "li"]):
        if _in_structure(el):
            continue
        children = el.find_all(True, recursive=False)
        own = "".join(el.find_all(string=True, recursive=False)).strip()

        inline = re.match(r"^([A-Za-zÄÖÜäöüß/. ]{2,30}):\s*(\S.*)$", own)
        if el.name in ("p", "li", "div", "span") and inline and (key := _label_key(inline.group(1))):
            if key not in fields and '"' not in inline.group(1):
                label = inline.group(1).strip()
                fields[key] = {"selector": f'{el.name}:-soup-contains-own("{label}:")',
                               "regex": rf"{re.escape(label)}:\s*(.+?){_NEXT_LABEL}"}
            continue

        if children or not own:
            continue
        key = _label_key(own)
        if not key or key in fields or '"' in own:
            continue
        core = re.sub(r"[:\s]+$", "", own)
        nxt = _next_content(el)
        if isinstance(nxt, Tag):
            fields[key] = {"selector": f'{el.name}:-soup-contains-own("{own}") + {nxt.name}'}
        elif nxt is not None and el.name in ("strong", "b", "span", "label"):
            fields[key] = {"selector": f'{el.name}:-soup-contains-own("{own}")', "up": 1,
                           "regex": rf"{re.escape(core)}:?\s*(.+?){_NEXT_LABEL}"}
    return fields


def detect_images(soup: BeautifulSoup) -> dict | None:
    bad = re.compile(r"logo|icon|sprite|banner|avatar|spacer|pixel|facebook|social|flag|emoji|placeholder", re.I)

    def good(img: Tag) -> bool:
        src = img.get("data-src") or img.get("data-lazy-src") or img.get("src") or ""
        if not src or src.startswith("data:") or bad.search(src) or bad.search(" ".join(img.get("class") or [])):
            return False
        if _in_structure(img):
            return False
        width = str(img.get("width", "")).strip()
        return not (width.isdigit() and int(width) < 80)

    for scope in ("main", "article", "#content", ".content", "#main", ".entry-content", "body"):
        imgs = [i for i in soup.select(f"{scope} img") if good(i)]
        if imgs:
            lazy = sum(1 for i in imgs if i.get("data-src")) > len(imgs) / 2
            return {"selector": f"{scope} img", "attr": "data-src" if lazy else "src"}
    return None


def detect_description(soup: BeautifulSoup) -> dict | None:
    scope = soup.select_one("main") or soup.select_one("article") or soup.body or soup
    paragraphs = [p for p in scope.find_all("p")
                  if len(p.get_text(" ", strip=True)) >= 80 and not _in_structure(p)]
    if not paragraphs:
        return None
    longest = max(paragraphs, key=lambda p: len(p.get_text(" ", strip=True)))
    for p in (longest, paragraphs[0]):
        for selector in (css_for(p), f"{scope.name} {css_for(p)}"):
            if soup.select_one(selector) is p:
                return {"selector": selector}
    return None


def detect_detail_fields(soup: BeautifulSoup) -> tuple[dict[str, dict], list[str]]:
    notes: list[str] = []
    fields: dict[str, dict] = {}
    heading = "h1" if soup.select_one("h1") else ("h2" if soup.select_one("h2") else None)
    if heading:
        fields["name"] = {"selector": heading}
    else:
        notes.append("TODO: no heading found on the detail page - set the 'name' selector by hand.")

    scope = soup.select_one("main") or soup.select_one("article") or soup.body or soup
    fields.update(detect_label_fields(scope))
    if images := detect_images(soup):
        fields["images"] = images
    if description := detect_description(soup):
        fields["description"] = description
    else:
        notes.append("No description paragraph recognized - add a 'description' selector if the page has one.")
    return fields, notes


# ------------------------------------------------------------------ orchestration

@dataclass
class Suggestion:
    config: dict
    notes: list[str] = field(default_factory=list)
    report: list[str] = field(default_factory=list)

    def to_yaml(self) -> str:
        header = ["# Generated by `tsc-crawler suggest`. REVIEW BEFORE USE:",
                  "#   tsc-crawler run <this file> --dry-run"]
        header += [f"# {line}" for line in self.notes]
        body = yaml.safe_dump(self.config, allow_unicode=True, sort_keys=False, width=1000)
        return "\n".join(header) + "\n\n" + body


def _compact_fields(fields: dict[str, dict]) -> dict:
    out = {}
    for key, spec in fields.items():
        spec = {k: v for k, v in spec.items() if v not in (None, 0, False)}
        out[key] = spec["selector"] if list(spec) == ["selector"] else spec
    return out


def suggest(url: str, fetcher: Fetcher, list_url: str | None = None, verify: bool = True) -> Suggestion:
    fetcher = CachingFetcher(fetcher)
    origin = f"{urlsplit(url).scheme}://{urlsplit(url).netloc}"
    notes: list[str] = []

    # 1) find the list page(s)
    home_soup: BeautifulSoup | None = None
    if list_url:
        candidates = [(100, list_url)]
    else:
        home = fetcher.get(url)
        if home is None:
            raise SuggestError(f"Could not fetch {url}")
        home_soup = parse_html(home)
        found = candidate_links(home_soup, url)
        found += [(s, u) for s, u in sitemap_candidates(fetcher, origin) if u not in {x[1] for x in found}]
        candidates = sorted(found, key=lambda x: -x[0])[:6]
        if not candidates:
            candidates = [(1, url)]  # maybe the start page itself lists the animals

    results: list[tuple[str, BeautifulSoup, ItemGroup]] = []
    for _, candidate in candidates:
        html = fetcher.get(candidate)
        if html is None:
            continue
        soup = parse_html(html)
        group = detect_items(soup)
        if group:
            results.append((candidate, soup, group))
    if not results:
        raise SuggestError("No animal list found. Pass the list page explicitly with --list-url.")

    list_page, soup, group = max(results, key=lambda r: r[2].score)
    start_urls = [list_page] + [u for u, _, g in results if u != list_page and g.selector == group.selector]
    if not group.exact:
        notes.append("The item selector also matches other elements - check the dry-run output.")

    # 2) detail page -> fields
    config: dict = {"name": _site_name(home_soup or soup, url), "start_urls": start_urls, "item_selector": group.selector}
    detail_selector = detect_detail_link(group.elements)
    detail_soup = None
    if detail_selector:
        config["detail_link_selector"] = detail_selector
        first = group.elements[0]
        link = first if first.name == "a" else first.select_one(detail_selector)
        detail_url = urljoin(list_page, _real_href(link) or "")
        detail_html = fetcher.get(detail_url)
        if detail_html:
            detail_soup = parse_html(detail_html)
    if detail_soup is not None:
        fields, field_notes = detect_detail_fields(detail_soup)
        notes += field_notes
    else:
        notes.append("No detail pages recognized - fields are read from the list items (list mode).")
        fields = _list_mode_fields(group.elements[0])
        config.pop("detail_link_selector", None)

    if (nxt := detect_next(soup)):
        config["next_page_selector"] = nxt
        config["max_pages"] = 20
    config["delay_seconds"] = 2
    config["min_animals"] = 3
    species = {normalize_species(urlsplit(u).path, "") for u in start_urls}
    if len(species) == 1 and "" not in species and "species" not in fields:
        config["default_species"] = species.pop()
        notes.append("default_species set from the URL path - correct it if the list mixes species.")
    config["fields"] = _compact_fields(fields)

    missing = [k for k in ("species", "age", "sex") if k not in fields and "default_species" not in config]
    if missing:
        notes.append(f"Not found on the page (check by hand): {', '.join(missing)}")
    notes.append("external_id defaults to the animal's URL slug; add an 'external_id' field if the page shows a stable number.")

    suggestion = Suggestion(config=config, notes=notes)
    if verify:
        suggestion.report = verify_config(config, fetcher)
    return suggestion


def _site_name(soup: BeautifulSoup, url: str) -> str:
    title = soup.title.get_text(strip=True) if soup.title else ""
    title = re.split(r"\s[|–—-]\s", title)[-1] if title else ""
    return title or urlsplit(url).netloc.removeprefix("www.")


def _list_mode_fields(item: Tag) -> dict[str, dict]:
    fields: dict[str, dict] = {}
    for tag in ("h1", "h2", "h3", "h4", "h5", "h6", "strong", "a"):
        if (el := item.find(tag)) and el.get_text(strip=True):
            fields["name"] = {"selector": css_for(el)}
            break
    if item.find("img"):
        fields["images"] = {"selector": "img", "attr": "src"}
    fields.update(detect_label_fields(item))
    return fields


def verify_config(config: dict, fetcher: Fetcher, sample: int = 5) -> list[str]:
    """Small test crawl of the draft: how many animals, which fields were filled."""
    raw = {**config, "max_items": sample, "max_pages": 1}
    try:
        result = crawl(parse_site(raw), fetcher)
    except Exception as exc:  # noqa: BLE001 - report instead of crashing the suggestion
        return [f"Verification failed: {exc}"]
    animals = result.animals
    report = [f"Test crawl: {len(animals)} animal(s), {result.errors} error(s)."]
    if not animals:
        report.append("Nothing found - the draft needs manual work.")
        return report
    checks = {
        "name": lambda a: a.name, "species": lambda a: a.species != "Sonstige",
        "age": lambda a: a.approx_age_months is not None, "sex": lambda a: a.sex != "Unbekannt",
        "size": lambda a: a.size != "Unbekannt", "images": lambda a: bool(a.image_urls),
        "description": lambda a: bool(a.description),
    }
    for label, check in checks.items():
        report.append(f"  {label:<12} {sum(1 for a in animals if check(a))}/{len(animals)}")
    report.append("Sample: " + ", ".join(a.name for a in animals[:3]))
    return report
