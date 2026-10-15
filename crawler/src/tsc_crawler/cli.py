"""Command line interface.

  tsc-crawler run config/sites/mein-tierheim.yaml --dry-run
  tsc-crawler run-all config/sites
  tsc-crawler detect https://www.beispiel-tierheim.de
  tsc-crawler suggest https://www.beispiel-tierheim.de --out config/beispiel-tierheim.yaml
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import sys
from pathlib import Path

from .client import ImportClient, ImportFailed
from .config import SiteConfig, load_site
from .detect import detect_cms
from .fetcher import HttpFetcher
from .runner import crawl
from .suggest import SuggestError, suggest

log = logging.getLogger("tsc_crawler")


def run_site(config: SiteConfig, api_url: str, dry_run: bool, limit: int | None) -> bool:
    fetcher = HttpFetcher(delay_seconds=config.delay_seconds, respect_robots=config.respect_robots)
    if limit:
        config.max_items = limit
    result = crawl(config, fetcher)
    snapshot = result.is_complete_snapshot(config)
    log.info("%s: %d animals, %d pages, %d errors, truncated=%s -> snapshot=%s",
             config.name, len(result.animals), result.pages, result.errors, result.truncated, snapshot)

    if dry_run:
        print(json.dumps([a.to_payload() for a in result.animals], ensure_ascii=False, indent=2))
        return True
    if not result.animals:
        log.error("%s: nothing found, nothing sent.", config.name)
        return False

    api_key = os.environ.get(config.api_key_env)
    if not api_key:
        log.error("Environment variable %s is not set (API key of the organization).", config.api_key_env)
        return False
    try:
        summary = ImportClient(api_url, api_key).push(result.animals, snapshot)
    except ImportFailed as exc:
        log.error("%s: %s", config.name, exc)
        return False
    log.info("%s: created=%d updated=%d unchanged=%d archived=%d", config.name,
             summary.created, summary.updated, summary.unchanged, summary.archived)
    return True


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="tsc-crawler")
    parser.add_argument("-v", "--verbose", action="store_true")
    sub = parser.add_subparsers(dest="command", required=True)

    for name in ("run", "run-all"):
        p = sub.add_parser(name)
        p.add_argument("path", help="site YAML file" if name == "run" else "directory with site YAML files")
        p.add_argument("--api-url", default=os.environ.get("TSC_API_URL", "http://localhost:5080"))
        p.add_argument("--dry-run", action="store_true", help="print the payload, do not send")
        p.add_argument("--limit", type=int, help="stop after N animals (also disables snapshot)")

    d = sub.add_parser("detect")
    d.add_argument("url")

    g = sub.add_parser("suggest", help="propose a site config for a shelter website")
    g.add_argument("url", help="home page of the shelter")
    g.add_argument("--list-url", help="skip the search and use this page as the animal list")
    g.add_argument("--out", help="write the YAML draft to this file instead of printing it")
    g.add_argument("--no-verify", action="store_true", help="skip the small test crawl")

    args = parser.parse_args(argv)
    logging.basicConfig(level=logging.DEBUG if args.verbose else logging.INFO,
                        format="%(asctime)s %(levelname)s %(message)s")

    if args.command == "detect":
        html = HttpFetcher(delay_seconds=0).get(args.url)
        if html is None:
            log.error("Could not fetch %s", args.url)
            return 1
        cms, version = detect_cms(html)
        print(f"{cms}{' ' + version if version else ''}")
        return 0

    if args.command == "suggest":
        try:
            result = suggest(args.url, HttpFetcher(delay_seconds=1.0), args.list_url, not args.no_verify)
        except SuggestError as exc:
            log.error("%s", exc)
            return 1
        text = result.to_yaml()
        if args.out:
            Path(args.out).write_text(text, encoding="utf-8")
            log.info("Draft written to %s", args.out)
        else:
            print(text)
        for line in result.report:
            log.info(line)
        log.info("Review the draft, then run: tsc-crawler run <file> --dry-run")
        return 0

    files = [Path(args.path)] if args.command == "run" else sorted(Path(args.path).glob("*.y*ml"))
    ok = True
    for file in files:
        ok &= run_site(load_site(file), args.api_url, args.dry_run, args.limit)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
