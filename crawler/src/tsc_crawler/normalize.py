"""Turns free German text from shelter websites into the enum values of the portal."""

from __future__ import annotations

import re
from datetime import date

_SPECIES_KEYWORDS: list[tuple[str, tuple[str, ...]]] = [
    ("Hund", ("hund", "rüde", "ruede", "hündin", "huendin", "welpe")),
    ("Katze", ("katze", "kater", "kitten", "kätzchen", "kaetzchen")),
    ("Kleintier", ("kaninchen", "meerschwein", "hamster", "ratte", "maus", "mäuse", "chinchilla",
                   "frettchen", "degu", "gerbil", "kleintier")),
    ("Vogel", ("vogel", "vögel", "papagei", "wellensittich", "kanarien", "nymphensittich", "sittich")),
    ("Pferd", ("pferd", "pony", "esel", "stute", "wallach")),
    ("Reptil", ("schildkröte", "schildkroete", "echse", "schlange", "gecko", "bartagame", "reptil")),
]


def normalize_species(text: str | None, default: str | None = None) -> str:
    if text:
        lowered = text.lower()
        best: tuple[int, str] | None = None
        for species, keywords in _SPECIES_KEYWORDS:
            for kw in keywords:
                pos = lowered.find(kw)
                if pos >= 0 and (best is None or pos < best[0]):
                    best = (pos, species)
        if best:
            return best[1]
    return default or "Sonstige"


def normalize_sex(text: str | None) -> str:
    if not text:
        return "Unbekannt"
    lowered = text.strip().lower()
    if lowered in {"m", "♂"}:
        return "Maennlich"
    if lowered in {"w", "f", "♀"}:
        return "Weiblich"
    # Check "weiblich" first: it contains "männlich"-like substrings in no case, but keep order explicit.
    if re.search(r"weiblich|hündin|huendin|stute|häsin|haesin", lowered):
        return "Weiblich"
    if re.search(r"männlich|maennlich|rüde|ruede|kater|rammler|hengst", lowered):
        return "Maennlich"
    return "Unbekannt"


def normalize_size(text: str | None) -> str:
    if not text:
        return "Unbekannt"
    lowered = text.lower()
    best: tuple[int, str] | None = None
    for size, pattern in (("Klein", r"klein"), ("Mittel", r"mittel"), ("Gross", r"gro(ß|ss)")):
        m = re.search(pattern, lowered)
        if m and (best is None or m.start() < best[0]):
            best = (m.start(), size)
    return best[1] if best else "Unbekannt"


_NEGATIVE = re.compile(
    r"\bnein\b|nicht\s+(?:verträglich|vertraeglich|geeignet|gut)|unverträglich|unvertraeglich|ungeeignet|\bkein")
_POSITIVE = re.compile(r"\bja\b|verträglich|vertraeglich|geeignet|\bgut\b|\bmag\b")


def normalize_compat(text: str | None) -> str:
    """Tri-state: Ja / Nein / Unbekannt. Negations are checked first ('nicht verträglich')."""
    if not text:
        return "Unbekannt"
    lowered = text.strip().lower()
    if _NEGATIVE.search(lowered):
        return "Nein"
    if _POSITIVE.search(lowered):
        return "Ja"
    return "Unbekannt"


def normalize_neutered(text: str | None) -> bool | None:
    if not text:
        return None
    lowered = text.strip().lower()
    if re.search(r"nicht\s+(kastriert|sterilisiert)|unkastriert|\bnein\b", lowered):
        return False
    if re.search(r"kastriert|sterilisiert|\bja\b", lowered):
        return True
    return None


_NUMBER = r"(\d{1,3}(?:\.\d{3})+(?:,\d+)?|\d+(?:[.,]\d+)?)"


def _parse_german_number(raw: str) -> float:
    if "," in raw:
        return float(raw.replace(".", "").replace(",", "."))
    if re.fullmatch(r"\d{1,3}(\.\d{3})+", raw):  # 1.200 -> thousands separator
        return float(raw.replace(".", ""))
    return float(raw)


def parse_fee(text: str | None) -> float | None:
    if not text:
        return None
    m = re.search(_NUMBER, text)
    return _parse_german_number(m.group(1)) if m else None


def parse_weight_kg(text: str | None) -> float | None:
    if not text:
        return None
    m = re.search(_NUMBER + r"\s*kg", text.lower())
    return _parse_german_number(m.group(1)) if m else None


def parse_age(text: str | None, today: date | None = None) -> tuple[int | None, date | None]:
    """Returns (approx_age_months, birth_date).

    Understands '3 Jahre', '2 Jahre 4 Monate', 'ca. 8 Monate', '12 Wochen',
    'geb. 05.03.2021' and 'geboren 03/2021'.
    """
    if not text:
        return None, None
    today = today or date.today()
    lowered = text.lower()

    m = re.search(r"(\d{1,2})\.(\d{1,2})\.(\d{4})", lowered)
    if m:
        day, month, year = (int(g) for g in m.groups())
        try:
            born = date(year, month, day)
            return _months_between(born, today), born
        except ValueError:
            pass
    m = re.search(r"(\d{1,2})[./](\d{4})", lowered)
    if m and ("geb" in lowered or "*" in lowered or len(lowered.strip()) <= 8):
        month, year = int(m.group(1)), int(m.group(2))
        if 1 <= month <= 12:
            born = date(year, month, 1)
            return _months_between(born, today), born

    months = 0.0
    found = False
    for pattern, factor in ((r"(\d+(?:[.,]\d+)?)\s*(?:jahr|jahre|jahren|j\b)", 12.0),
                            (r"(\d+(?:[.,]\d+)?)\s*(?:monat|monate|monaten|mon\b|m\b)", 1.0),
                            (r"(\d+(?:[.,]\d+)?)\s*(?:woche|wochen|wo\b)", 1 / 4.345)):
        for match in re.finditer(pattern, lowered):
            months += float(match.group(1).replace(",", ".")) * factor
            found = True
    if found:
        return round(months), None
    return None, None


def _months_between(born: date, today: date) -> int:
    months = (today.year - born.year) * 12 + (today.month - born.month)
    if today.day < born.day:
        months -= 1
    return max(months, 0)


def slugify(text: str) -> str:
    """Stable ASCII slug, used as a last-resort external id."""
    table = str.maketrans({"ä": "ae", "ö": "oe", "ü": "ue", "ß": "ss"})
    ascii_text = text.lower().translate(table)
    return re.sub(r"[^a-z0-9]+", "-", ascii_text).strip("-") or "tier"


def clean_text(text: str | None) -> str | None:
    if text is None:
        return None
    cleaned = re.sub(r"\s+", " ", text).strip()
    return cleaned or None
