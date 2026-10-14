"""Detects which CMS / website builder a site runs on.

Useful to decide whether a CMS plugin (e.g. WordPress) could replace crawling for an organization.
"""

from __future__ import annotations

import re

from .extract import parse_html

# (name, regex on raw HTML, optional regex on the generator meta tag)
_SIGNATURES: list[tuple[str, str]] = [
    ("WordPress", r"/wp-content/|/wp-includes/|wp-json"),
    ("TYPO3", r"typo3conf/|typo3temp/|/typo3/"),
    ("Joomla", r"/media/jui/|/media/system/js/|com_content"),
    ("Drupal", r"/sites/default/files/|drupal-settings-json"),
    ("Wix", r"static\.parastorage\.com|wixstatic\.com"),
    ("Jimdo", r"jimdo(free|cdn)?\.com|jimdosite"),
    ("Squarespace", r"squarespace\.com|static1\.squarespace"),
    ("Shopware", r"shopware"),
]

_GENERATOR = {
    "wordpress": "WordPress",
    "typo3": "TYPO3",
    "joomla": "Joomla",
    "drupal": "Drupal",
    "wix": "Wix",
    "jimdo": "Jimdo",
    "squarespace": "Squarespace",
}


def detect_cms(html: str) -> tuple[str, str | None]:
    """Returns (cms_name, version). cms_name is 'Unbekannt' if nothing matched."""
    soup = parse_html(html)
    meta = soup.find("meta", attrs={"name": re.compile("^generator$", re.I)})
    content = (meta.get("content") or "") if meta else ""
    lowered = content.lower()
    for key, name in _GENERATOR.items():
        if key in lowered:
            version = re.search(r"(\d+(?:\.\d+)+)", content)
            return name, version.group(1) if version else None
    for name, pattern in _SIGNATURES:
        if re.search(pattern, html, re.I):
            return name, None
    return "Unbekannt", None
