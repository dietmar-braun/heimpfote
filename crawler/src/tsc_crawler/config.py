"""Site configuration: one YAML file per shelter website."""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path

import yaml


@dataclass
class FieldSpec:
    selector: str | None = None
    attr: str | None = None       # None = element text
    regex: str | None = None      # first capture group (or whole match) is used
    multiple: bool = False        # collect all matches (e.g. images)
    up: int = 0                   # climb N parents from the matched element (label/value pairs)
    static: str | None = None     # constant value instead of a selector


@dataclass
class SiteConfig:
    name: str
    start_urls: list[str]
    item_selector: str
    fields: dict[str, FieldSpec]
    detail_link_selector: str | None = None   # list page -> detail page mode
    next_page_selector: str | None = None
    max_pages: int = 20
    max_items: int = 500                       # import API accepts max. 500 per request
    min_animals: int = 1                       # snapshot safety net, see runner
    delay_seconds: float = 2.0
    respect_robots: bool = True
    default_species: str | None = None
    api_key_env: str = "TSC_API_KEY"
    extra: dict = field(default_factory=dict)


_ALLOWED_FIELDS = {
    "external_id", "name", "species", "breed", "sex", "age", "size", "weight", "color", "neutered",
    "description", "fee", "images", "children", "dogs", "cats", "apartment",
}


def load_site(path: str | Path) -> SiteConfig:
    raw = yaml.safe_load(Path(path).read_text(encoding="utf-8"))
    return parse_site(raw)


def parse_site(raw: dict) -> SiteConfig:
    for key in ("name", "start_urls", "item_selector", "fields"):
        if key not in raw:
            raise ValueError(f"Missing required key '{key}' in site config")

    fields_raw = raw["fields"]
    unknown = set(fields_raw) - _ALLOWED_FIELDS
    if unknown:
        raise ValueError(f"Unknown field(s) in config: {', '.join(sorted(unknown))}")
    if "name" not in fields_raw:
        raise ValueError("Field 'name' is required")

    specs = {k: FieldSpec(**v) if isinstance(v, dict) else FieldSpec(selector=v)
             for k, v in fields_raw.items()}
    optional = {k: raw[k] for k in (
        "detail_link_selector", "next_page_selector", "max_pages", "max_items", "min_animals",
        "delay_seconds", "respect_robots", "default_species", "api_key_env") if k in raw}
    start_urls = raw["start_urls"]
    if isinstance(start_urls, str):
        start_urls = [start_urls]
    return SiteConfig(name=raw["name"], start_urls=start_urls, item_selector=raw["item_selector"],
                      fields=specs, **optional)
