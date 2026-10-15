"""Polite HTTP fetching: robots.txt, per-host delay, retries."""

from __future__ import annotations

import logging
import time
from typing import Protocol
from urllib.parse import urlsplit
from urllib.robotparser import RobotFileParser

import requests
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry

log = logging.getLogger(__name__)

USER_AGENT = "TierschutzCollectBot/0.1 (+https://github.com/; adoption-listing aggregator)"


class Fetcher(Protocol):
    def get(self, url: str) -> str | None:
        """Returns the HTML or None if the page must not or cannot be fetched."""


class HttpFetcher:
    def __init__(self, delay_seconds: float = 2.0, respect_robots: bool = True,
                 timeout: float = 20.0, session: requests.Session | None = None):
        self.delay = delay_seconds
        self.respect_robots = respect_robots
        self.timeout = timeout
        self.session = session or requests.Session()
        self.session.headers["User-Agent"] = USER_AGENT
        retry = Retry(total=3, backoff_factor=1.5, status_forcelist=(429, 500, 502, 503, 504),
                      allowed_methods=("GET",), respect_retry_after_header=True)
        self.session.mount("http://", HTTPAdapter(max_retries=retry))
        self.session.mount("https://", HTTPAdapter(max_retries=retry))
        self._robots: dict[str, RobotFileParser | None] = {}
        self._last_request: dict[str, float] = {}

    def _robots_for(self, url: str) -> RobotFileParser | None:
        parts = urlsplit(url)
        origin = f"{parts.scheme}://{parts.netloc}"
        if origin not in self._robots:
            parser = RobotFileParser()
            try:
                resp = self.session.get(f"{origin}/robots.txt", timeout=self.timeout)
                if resp.status_code == 200:
                    parser.parse(resp.text.splitlines())
                    self._robots[origin] = parser
                else:
                    self._robots[origin] = None  # no robots.txt = everything allowed
            except requests.RequestException:
                self._robots[origin] = None
        return self._robots[origin]

    def allowed(self, url: str) -> bool:
        if not self.respect_robots:
            return True
        parser = self._robots_for(url)
        return parser is None or parser.can_fetch(USER_AGENT, url)

    def get(self, url: str) -> str | None:
        return self._get(url, require_html=True)

    def get_raw(self, url: str) -> str | None:
        """Like get(), but accepts any content type (e.g. sitemap.xml)."""
        return self._get(url, require_html=False)

    def _get(self, url: str, require_html: bool) -> str | None:
        if not self.allowed(url):
            log.warning("Blocked by robots.txt: %s", url)
            return None
        host = urlsplit(url).netloc
        wait = self.delay - (time.monotonic() - self._last_request.get(host, 0.0))
        if wait > 0:
            time.sleep(wait)
        try:
            resp = self.session.get(url, timeout=self.timeout)
            self._last_request[host] = time.monotonic()
            resp.raise_for_status()
        except requests.RequestException as exc:
            log.error("Fetch failed for %s: %s", url, exc)
            return None
        if require_html and "html" not in resp.headers.get("Content-Type", "html").lower():
            log.warning("Skipping non-HTML response: %s", url)
            return None
        resp.encoding = resp.encoding or resp.apparent_encoding
        return resp.text
