# -*- coding: utf-8 -*-
"""Check every language catalogue against the English source.

Run from the project root:  python scripts/validate-languages.py
Add --complete to also require that every language translates every key.

Two kinds of finding, and the difference matters:

ERRORS, always fatal. A key the code does not know, and a changed set of {0}-style
placeholders. The second is the dangerous one: a template whose placeholders do not match
its call site throws at runtime or drops information silently.

COVERAGE, reported but not fatal. Keys a language does not translate yet. The layer is
designed so those fall back to English - lang/english.txt promises exactly that to
translators, and the compiled defaults sit behind both files - so a partial catalogue is a
working catalogue, and blocking on it would mean no key could ever be added without
touching fifteen files in the same change. Pass --complete to make it fatal, which is what
a release should do.

"same-as-en" counts lines that were copied rather than translated. Those should normally be
deleted so they fall back explicitly instead of inflating the coverage figure.
"""
import io, os, re, sys

ph = re.compile(r'\{(\d+)\}')
require_complete = '--complete' in sys.argv

en = {}
for line in io.open('lang/english.txt', encoding='utf-8'):
    t = line.strip()
    if not t or t.startswith('#') or '=' not in t: continue
    k, v = t.split('=', 1); en[k.strip()] = v.strip()

bad = 0
incomplete = 0
for fn in sorted(os.listdir('lang')):
    if not fn.endswith('.txt'): continue
    keys = {}; meta = {}
    for line in io.open(os.path.join('lang', fn), encoding='utf-8'):
        t = line.strip()
        if not t or t.startswith('#') or '=' not in t: continue
        k, v = t.split('=', 1); k = k.strip(); v = v.strip()
        (meta if k.startswith('_meta.') else keys)[k] = v

    missing = [k for k in en if k not in keys]
    unknown = [k for k in keys if k not in en]
    phbad = []
    untranslated = []
    for k, v in keys.items():
        if k in en and sorted(ph.findall(v)) != sorted(ph.findall(en[k])): phbad.append(k)
        if fn != 'english.txt' and k in en and v == en[k]: untranslated.append(k)

    status = meta.get('_meta.status', '(none)') if fn != 'english.txt' else 'source'
    coverage = 100.0 * (len(en) - len(missing)) / len(en) if en else 100.0

    flag = ''
    if unknown or phbad:
        flag = ' <-- ERROR'; bad += 1
    elif missing:
        flag = ' <-- incomplete'
        incomplete += 1
        if require_complete: bad += 1

    print("%-26s keys=%3d status=%-14s coverage=%5.1f%% missing=%3d unknown=%d "
          "placeholder-mismatch=%d same-as-en=%d%s"
          % (fn, len(keys), status, coverage, len(missing), len(unknown), len(phbad),
             len(untranslated), flag))
    for k in unknown: print("      not a known key: %s" % k)
    for k in phbad: print("      placeholders differ: %s -> %s" % (k, keys[k]))
    for k in untranslated: print("      identical to English: %s" % k)

print()
if incomplete and not require_complete:
    print("%d catalogue(s) incomplete. Those keys fall back to English, which is by design; "
          "run with --complete to treat it as a failure." % incomplete)

sys.exit(1 if bad else 0)
