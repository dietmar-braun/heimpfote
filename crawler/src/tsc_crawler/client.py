"""Pushes crawled animals to the portal import API."""

from __future__ import annotations

import logging
from dataclasses import dataclass

import requests

from .models import Animal

log = logging.getLogger(__name__)

MAX_BATCH_SIZE = 500  # keep in sync with ImportValidator.MaxBatchSize in the portal


class ImportFailed(RuntimeError):
    pass


@dataclass
class ImportSummary:
    created: int = 0
    updated: int = 0
    unchanged: int = 0
    archived: int = 0


class ImportClient:
    def __init__(self, api_url: str, api_key: str, session: requests.Session | None = None,
                 timeout: float = 60.0):
        self.endpoint = api_url.rstrip("/") + "/api/import/animals"
        self.session = session or requests.Session()
        self.session.headers["X-Api-Key"] = api_key
        self.timeout = timeout

    def push(self, animals: list[Animal], is_full_snapshot: bool) -> ImportSummary:
        if not animals:
            return ImportSummary()
        if len(animals) > MAX_BATCH_SIZE and is_full_snapshot:
            # A snapshot must arrive in ONE request, otherwise the second batch would archive the first.
            log.warning("More than %d animals: sending without snapshot, nothing will be archived.",
                        MAX_BATCH_SIZE)
            is_full_snapshot = False

        total = ImportSummary()
        for start in range(0, len(animals), MAX_BATCH_SIZE):
            batch = animals[start:start + MAX_BATCH_SIZE]
            body = {"source": "Crawler", "isFullSnapshot": is_full_snapshot,
                    "animals": [a.to_payload() for a in batch]}
            resp = self.session.post(self.endpoint, json=body, timeout=self.timeout)
            if resp.status_code != 200:
                raise ImportFailed(f"Import failed ({resp.status_code}): {resp.text[:500]}")
            data = resp.json()
            total.created += data.get("created", 0)
            total.updated += data.get("updated", 0)
            total.unchanged += data.get("unchanged", 0)
            total.archived += data.get("archived", 0)
        return total
