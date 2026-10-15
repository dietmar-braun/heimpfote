"""Selector-based extraction of one animal from an HTML element."""

from __future__ import annotations

import re
from datetime import date
from urllib.parse import urljoin, urlsplit

from bs4 import BeautifulSoup
from bs4.element import Tag

from .config import FieldSpec, SiteConfig
from .models import Animal
from . import normalize as n


def parse_html(html: str) -> BeautifulSoup:
    return BeautifulSoup(html, "lxml")


def _raw_values(scope: Tag, spec: FieldSpec) -> list[str]:
    if spec.static is not None:
        return [spec.static]
    if not spec.selector:
        return []
    elements = scope.select(spec.selector)
    if not spec.multiple:
        elements = elements[:1]
    values: list[str] = []
    for el in elements:
        for _ in range(spec.up):
            if el.parent is not None:
                el = el.parent
        if spec.attr:
            value = el.get(spec.attr)
            if isinstance(value, list):
                value = " ".join(value)
        else:
            value = el.get_text(" ", strip=True)
        if value is None:
            continue
        if spec.regex:
            m = re.search(spec.regex, value)
            if not m:
                continue
            value = m.group(1) if m.groups() else m.group(0)
        value = value.strip()
        if value:
            values.append(value)
    return values


def _first(scope: Tag, config: SiteConfig, key: str) -> str | None:
    spec = config.fields.get(key)
    if spec is None:
        return None
    values = _raw_values(scope, spec)
    return values[0] if values else None


def _slug_from_url(url: str) -> str:
    path = urlsplit(url).path.rstrip("/")
    return path.rsplit("/", 1)[-1] or urlsplit(url).netloc


def extract_animal(scope: Tag, config: SiteConfig, page_url: str, today: date | None = None,
                   item_url: str | None = None) -> Animal | None:
    """Returns None if the element has no usable name.

    `item_url` is the animal's own page (detail page, or the link inside a list item). It becomes the
    source URL and, without an explicit external_id field, the stable id.
    """
    name = n.clean_text(_first(scope, config, "name"))
    if not name:
        return None

    # The external id must stay stable between runs: explicit field, then the animal's URL, then the name.
    external_id = _first(scope, config, "external_id") or (
        _slug_from_url(item_url) if item_url else n.slugify(name))

    species_text = _first(scope, config, "species")
    species = n.normalize_species(species_text, config.default_species)
    if species_text is None and config.default_species is None:
        species = n.normalize_species(name)  # last resort, e.g. "Kater Felix"

    age_months, birth_date = n.parse_age(_first(scope, config, "age"), today)

    breed = n.clean_text(_first(scope, config, "breed"))
    mixed = bool(breed and re.search(r"misch|mix|\bmk\b", breed.lower()))

    images: list[str] = []
    img_spec = config.fields.get("images")
    if img_spec:
        spec = FieldSpec(**{**img_spec.__dict__, "multiple": True})
        for src in _raw_values(scope, spec):
            absolute = urljoin(page_url, src)
            if absolute.startswith(("http://", "https://")) and absolute not in images:
                images.append(absolute)

    description = _first(scope, config, "description")

    return Animal(
        external_id=external_id[:200],
        name=name[:200],
        species=species,
        breed_name=breed,
        is_mixed_breed=mixed,
        sex=n.normalize_sex(_first(scope, config, "sex")),
        birth_date=birth_date,
        approx_age_months=age_months,
        size=n.normalize_size(_first(scope, config, "size")),
        weight_kg=n.parse_weight_kg(_first(scope, config, "weight")),
        color=n.clean_text(_first(scope, config, "color")),
        is_neutered=n.normalize_neutered(_first(scope, config, "neutered")),
        description=n.clean_text(description),
        adoption_fee=n.parse_fee(_first(scope, config, "fee")),
        good_with_children=n.normalize_compat(_first(scope, config, "children")),
        good_with_dogs=n.normalize_compat(_first(scope, config, "dogs")),
        good_with_cats=n.normalize_compat(_first(scope, config, "cats")),
        suitable_for_apartment=n.normalize_compat(_first(scope, config, "apartment")),
        source_url=item_url or page_url,
        image_urls=images,
    )
