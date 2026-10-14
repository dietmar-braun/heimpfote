"""Data model that mirrors AnimalImportDto of the portal import API."""

from __future__ import annotations

from dataclasses import dataclass, field, fields
from datetime import date


def _to_camel(name: str) -> str:
    head, *tail = name.split("_")
    return head + "".join(part.capitalize() for part in tail)


@dataclass
class Animal:
    external_id: str
    name: str
    species: str = "Sonstige"
    breed_name: str | None = None
    is_mixed_breed: bool = False
    sex: str = "Unbekannt"
    birth_date: date | None = None
    approx_age_months: int | None = None
    size: str = "Unbekannt"
    weight_kg: float | None = None
    color: str | None = None
    is_neutered: bool | None = None
    description: str | None = None
    adoption_fee: float | None = None
    status: str = "Vermittelbar"
    is_urgent: bool = False
    needs_experienced_owner: bool = False
    has_special_needs: bool = False
    good_with_children: str = "Unbekannt"
    good_with_dogs: str = "Unbekannt"
    good_with_cats: str = "Unbekannt"
    suitable_for_apartment: str = "Unbekannt"
    source_url: str | None = None
    image_urls: list[str] = field(default_factory=list)

    def to_payload(self) -> dict:
        """JSON body for the import API (camelCase, None values omitted)."""
        payload: dict = {}
        for f in fields(self):
            value = getattr(self, f.name)
            if value is None:
                continue
            if isinstance(value, date):
                value = value.isoformat()
            payload[_to_camel(f.name)] = value
        return payload
