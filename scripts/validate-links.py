# -*- coding: utf-8 -*-
"""Check every URL the project hands to a user or fetches at runtime.

Run from the project root:  python scripts/validate-links.py

This exists because a fabricated URL shipped in tester instructions and was a 404, and
because the download it was paired with silently resolved a different package than intended.
No written rule caught that. This does.

A URL that 404s, or that cannot be reached, fails the run. Redirects are followed and
reported, since a redirect is often the difference between a package and its namesake.
"""
import io, os, re, sys
try:
    from urllib.request import urlopen, Request
    from urllib.error import HTTPError, URLError
except ImportError:
    print("python 3 required"); sys.exit(2)

TARGETS = ['packaging', 'README.md', 'INSTALL.txt', 'docs']
URL = re.compile(r'https?://[^\s)\'"<>\]`]+')

def load_ignores():
    path = os.path.join('scripts', 'link-check-ignore.txt')
    ignores = {}
    if not os.path.isfile(path):
        return ignores
    for line in io.open(path, encoding='utf-8'):
        line = line.strip()
        if not line or line.startswith('#') or '=' not in line:
            continue
        url, reason = line.split('=', 1)
        ignores[url.strip()] = reason.strip()
    return ignores

def collect():
    found = {}
    for target in TARGETS:
        if os.path.isfile(target):
            paths = [target]
        elif os.path.isdir(target):
            paths = [os.path.join(r, f) for r, _, fs in os.walk(target) for f in fs
                     if f.lower().endswith(('.txt', '.md', '.ps1', '.cmd'))]
        else:
            continue
        for p in paths:
            try:
                text = io.open(p, encoding='utf-8', errors='replace').read()
            except OSError:
                continue
            for m in URL.findall(text):
                found.setdefault(m.rstrip('.,;'), set()).add(p.replace(os.sep, '/'))
    return found

def check(url):
    req = Request(url, headers={'User-Agent': 'CultAccess-link-check'})
    try:
        with urlopen(req, timeout=30) as r:
            return r.status, r.geturl()
    except HTTPError as e:
        return e.code, url
    except URLError as e:
        return None, str(e.reason)

def main():
    urls = collect()
    ignores = load_ignores()
    if not urls:
        print("No URLs found. Check TARGETS."); return 1

    failures = 0
    for url in sorted(urls):
        if url in ignores:
            print("  DEAD (expected) %s" % url)
            print("       %s" % ignores[url])
            continue
        status, final = check(url)
        where = ', '.join(sorted(urls[url]))
        if status == 200:
            note = '' if final.rstrip('/') == url.rstrip('/') else ('  -> ' + final)
            print("  OK   %s%s" % (url, note))
        else:
            failures += 1
            print("  FAIL %s  (%s)" % (url, status if status else final))
            print("       referenced by: %s" % where)
    print("\n%d URL(s) checked, %d failed." % (len(urls), failures))
    return 1 if failures else 0

if __name__ == '__main__':
    sys.exit(main())
