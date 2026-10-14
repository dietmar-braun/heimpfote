"""Crawl one site: list pages (with pagination) and optional detail pages."""

from __future__ import annotations

import logging
from dataclasses import dataclass, field
from datetime import date
from urllib.parse import urljoin

from .config import SiteConfig
from .extract import extract_animal, parse_html
from .fetcher import Fetcher
from .models import Animal

log = logging.getLogger(__name__)


@dataclass
class CrawlResult:
    animals: list[Animal] = field(default_factory=list)
    errors: int = 0
    truncated: bool = False   # stopped by max_pages / max_items before the end
    pages: int = 0

    def is_complete_snapshot(self, config: SiteConfig) -> bool:
        """A snapshot makes the portal archive every animal that is missing from it.

        So it is only safe if the crawl saw the whole site: no fetch errors, not truncated,
        and at least `min_animals` animals (protects against a layout change that yields nothing).
        """
        return (self.errors == 0 and not self.truncated
                and len(self.animals) >= max(config.min_animals, 1))


def crawl(config: SiteConfig, fetcher: Fetcher, today: date | None = None) -> CrawlResult:
    result = CrawlResult()
    seen_pages: set[str] = set()
    seen_ids: set[str] = set()
    queue = list(config.start_urls)

    while queue:
        url = queue.pop(0)
        if url in seen_pages:
            continue
        if result.pages >= config.max_pages:
            result.truncated = True
            break
        seen_pages.add(url)

        html = fetcher.get(url)
        result.pages += 1
        if html is None:
            result.errors += 1
            continue
        soup = parse_html(html)

        for item in soup.select(config.item_selector):
            if len(result.animals) >= config.max_items:
                result.truncated = True
                return result
            animal = _read_item(item, url, config, fetcher, today, result)
            if animal is None:
                continue
            if animal.external_id in seen_ids:
                continue  # same animal linked twice (e.g. teaser + list)
            seen_ids.add(animal.external_id)
            result.animals.append(animal)

        if config.next_page_selector:
            nxt = soup.select_one(config.next_page_selector)
            href = nxt.get("href") if nxt else None
            if href:
                queue.append(urljoin(url, href))

    return result


def _read_item(item, list_url: str, config: SiteConfig, fetcher: Fetcher,
               today: date | None, result: CrawlResult) -> Animal | None:
    if not config.detail_link_selector:
        # List mode: all fields are on the list page. A link inside the item (if any) identifies the animal.
        own_link = item if item.name == "a" else item.select_one("a[href]")
        own_href = own_link.get("href") if own_link else None
        item_url = urljoin(list_url, own_href) if own_href and not own_href.startswith("#") else None
        return extract_animal(item, config, list_url, today, item_url=item_url)

    link = item.select_one(config.detail_link_selector)
    if item.name == "a" and link is None:
        link = item
    href = link.get("href") if link else None
    if not href:
        log.warning("No detail link found in item on %s", list_url)
        result.errors += 1
        return None
    detail_url = urljoin(list_url, href)
    html = fetcher.get(detail_url)
    if html is None:
        result.errors += 1
        return None
    animal = extract_animal(parse_html(html), config, detail_url, today, item_url=detail_url)
    if animal is None:
        log.warning("No animal name found on %s (selectors outdated?)", detail_url)
        result.errors += 1
    return animal
