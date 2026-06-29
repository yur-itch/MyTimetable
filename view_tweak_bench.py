#!/usr/bin/env python3
"""
view_tweak_bench.py — find the best-compressing combination of view tweaks,
measured with the EXACT compression the repo ships.

The repo compresses responses with .NET `BrotliStream(CompressionLevel.Optimal)`
(see MyTimetable/Compression.cs). That maps to brotli *quality 4* — NOT q11.
This harness therefore measures every variant through the real .NET code path
(brotli_net.cs) instead of the brotli CLI at -q 11.

It does NOT touch the original .cshtml views. It re-creates their output in
Python, applies each tweak combination, writes the variants to a temp folder,
and asks brotli_net.cs to compress them all in one process.

Tweaks explored (the toggles you'd actually apply to the views):
  * skip_empty : drop days that have no lessons
  * css_rule   : one `[id^="day-"]{display:contents}` rule + bare ids,
                 vs. the current inline `style="display:contents"` per day
  * minify     : minified <style> block vs. the pretty one
  * titles     : abbreviated (current) vs. full subject names
"""
import json, itertools, subprocess, os, sys, tempfile, shutil
from datetime import date

# Windows consoles default to cp1251 here; emit UTF-8 so the report renders.
_reconfigure = getattr(sys.stdout, "reconfigure", None)
if _reconfigure:
    _reconfigure(encoding="utf-8")

BASE   = os.path.dirname(os.path.abspath(__file__))
BROTLI_NET = os.path.join(BASE, "brotli_net.cs")

with open(os.path.join(BASE, "schedule_raw.json"), encoding="utf-8") as f:
    data = json.load(f)

# ── abbreviations (mirrors GetOne.cshtml's Abbrevs) ─────────────────────────────
ABBREVS = {
    "Физическая культура и спорт":                                              "Физра",
    "Элективные дисциплины по физической культуре и спорту":                    "Физра",
    "Алгебра и геометрия":                                                      "Алгем",
    "Критическое мышление в ИТ":                                                "Критмыш",
    "Математический анализ":                                                    "Матан",
    "Ознакомительная практика (учебная)":                                       "Практика",
    "Основы программирования":                                                  "Прога",
    "Основы дискретной логики и теории доказательств":                          "МКН1",
    "Основы российской государственности":                                      "ОРГ",
    "Парадигмы программирования":                                               "Парадигмы",
    "Погружение в университетскую среду":                                       "Погружение",
    "Практикум по программированию":                                            "Прога",
    "Безопасность жизнедеятельности":                                           "БЖД",
    "Введение в программирование":                                              "Прога",
    "Дискретная теория вероятности и основы математической статистики":         "МКН3",
    "Дискретные структуры":                                                     "МКН2",
    "Иностранный язык":                                                         "Английский",
    "История России":                                                           "История",
}
GROUP_NAMES = {"972501", "972501 (1)"}
WEEKDAYS    = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"]

def collapse(s):  return " ".join((s or "").split())
def cap(s):       return s[0].upper() + s[1:] if s else ""
def prof(name):
    if not name or not name.strip(): return ""
    return cap(name.split()[0].lower())
def room(aud):
    if not aud: return ""
    s = collapse(aud.get("shortName") or "")
    return s or collapse(aud.get("name") or "")
def accent(t):
    return {"PRACTICE":"#3b82f6","SEMINAR":"#f59e0b","LECTURE":"#e11d48"}.get(
        t, "#a855f7" if t in ("EXAM","CONTROL_POINT","DIFFERENTIAL_CREDIT","CREDIT")
        else ("#ec4899" if t=="CONSULTATION" else "#6b7280"))
def accent_bg(t):
    return {"PRACTICE":"#11294d","SEMINAR":"#3a2410","LECTURE":"#3d1320"}.get(
        t, "#2a1542" if t in ("EXAM","CONTROL_POINT","DIFFERENTIAL_CREDIT","CREDIT")
        else ("#3a132b" if t=="CONSULTATION" else "#1f232b"))

# ── process raw data (same selection as the controller's warm cache) ────────────
day_map    = {}
slots_used = set()
for day in data["grid"]:
    ds   = day["date"]
    dmap = {}
    for l in day["lessons"]:
        if l.get("type") != "LESSON": continue
        groups = l.get("groups") or []
        if not any(collapse(g.get("name","")).upper() in GROUP_NAMES for g in groups): continue
        num = l.get("lessonNumber", 0)
        if not (1 <= num <= 6): continue
        raw_title = cap(collapse(l.get("title","")))
        dmap.setdefault(num, []).append({
            "title_raw": raw_title,
            "prof": prof((l.get("professor") or {}).get("fullName")),
            "room": room(l.get("audience")),
            "type": collapse(l.get("lessonType","")).upper(),
        })
        slots_used.add(num)
    day_map[ds] = dmap

slots = sorted(slots_used)
N     = len(slots)

# Inject a 2nd (consultation) lesson into the first occupied slot, matching the
# real-world case of stacked lessons in one cell (same fixture as brotli_test.py).
for ds, dmap in day_map.items():
    for num in list(dmap.keys()):
        if len(dmap[num]) == 1:
            custom = dict(dmap[num][0])
            custom["title_raw"] = "Консультация"
            custom["type"] = "CONSULTATION"
            custom["room"] = "305"
            dmap[num].append(custom)
            break
    else:
        continue
    break

# ── CSS — aligned to the CURRENT Get.cshtml (row-min 92, content-visibility) ─────
CSS_PRETTY = f"""\
        :root {{
            --bg: #0a0a0f;
            --line: #1b1b24;
            --fg: #e8e8ef;
            --sub: #6b6b7b;
            --row-min: 92px;
            --day-w: 58px;
        }}

        * {{ box-sizing: border-box; }}

        html, body {{
            margin: 0;
            height: 100%;
            background: var(--bg);
            color: var(--fg);
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Arial, sans-serif;
            -webkit-font-smoothing: antialiased;
        }}

        #scroller {{ height: 100vh; overflow: auto; }}

        .grid {{
            display: grid;
            grid-template-columns: var(--day-w) repeat({N}, minmax(110px, 1fr));
            border-top: 1px solid var(--line);
            border-left: 1px solid var(--line);
        }}

        .cell {{
            border-right: 1px solid var(--line);
            border-bottom: 1px solid var(--line);
        }}

        .s-top  {{ position: sticky; top: 0;  z-index: 5;  background: var(--bg); }}
        .s-left {{ position: sticky; left: 0; z-index: 5;  background: var(--bg); }}
        .s-both {{ position: sticky; top: 0; left: 0; z-index: 10; background: var(--bg); }}

        .slot-head {{
            padding: 6px 8px;
            min-height: 46px;
            display: flex;
            flex-direction: column;
            justify-content: center;
        }}
        .slot-start {{ font-size: 13px; font-weight: 600; }}
        .slot-end   {{ font-size: 12px; color: var(--sub); margin-top: 1px; }}

        .day-label {{
            padding: 8px 6px;
            height: var(--row-min);
            display: flex;
            flex-direction: column;
        }}
        .day-name {{ font-size: 16px; font-weight: 600; }}
        .day-date {{ font-size: 16px; color: var(--sub); margin-top: 2px; }}

        .day-cell {{
            padding: 5px;
            height: var(--row-min);
            overflow: hidden;
            display: flex;
            flex-direction: column;
            gap: 5px;
        }}

        .lesson {{
            border-radius: 5px;
            border-left: 4px solid var(--accent);
            background: var(--accent-bg);
            padding: 7px 9px;
            flex: 1 1 auto;
            overflow: hidden;
            cursor: pointer;
        }}
        .lesson.hidden {{ opacity: .32; }}
        .lesson-title {{ font-size: 13px; font-weight: 600; line-height: 1.25; color: #f3f3f8; }}
        .lesson-prof  {{ font-size: 12px; margin-top: 3px; color: rgba(255,255,255,.62); }}
        .lesson-room  {{ font-size: 12px; margin-top: 2px; color: rgba(255,255,255,.45); }}

        .day-cell {{ content-visibility: auto; contain-intrinsic-size: 92px; }}"""

CSS_MINI = (
    f':root{{--bg:#0a0a0f;--line:#1b1b24;--fg:#e8e8ef;--sub:#6b6b7b;--row-min:92px;--day-w:58px}}'
    f'*{{box-sizing:border-box}}'
    f'html,body{{margin:0;height:100%;background:var(--bg);color:var(--fg);'
    f'font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Arial,sans-serif;'
    f'-webkit-font-smoothing:antialiased}}'
    f'#scroller{{height:100vh;overflow:auto}}'
    f'.grid{{display:grid;grid-template-columns:var(--day-w) repeat({N},minmax(110px,1fr));'
    f'border-top:1px solid var(--line);border-left:1px solid var(--line)}}'
    f'.cell{{border-right:1px solid var(--line);border-bottom:1px solid var(--line)}}'
    f'.s-top{{position:sticky;top:0;z-index:5;background:var(--bg)}}'
    f'.s-left{{position:sticky;left:0;z-index:5;background:var(--bg)}}'
    f'.s-both{{position:sticky;top:0;left:0;z-index:10;background:var(--bg)}}'
    f'.slot-head{{padding:6px 8px;min-height:46px;display:flex;flex-direction:column;justify-content:center}}'
    f'.slot-start{{font-size:13px;font-weight:600}}'
    f'.slot-end{{font-size:12px;color:var(--sub);margin-top:1px}}'
    f'.day-label{{padding:8px 6px;height:var(--row-min);display:flex;flex-direction:column}}'
    f'.day-name{{font-size:16px;font-weight:600}}'
    f'.day-date{{font-size:16px;color:var(--sub);margin-top:2px}}'
    f'.day-cell{{padding:5px;height:var(--row-min);overflow:hidden;display:flex;flex-direction:column;gap:5px}}'
    f'.lesson{{border-radius:5px;border-left:4px solid var(--accent);background:var(--accent-bg);'
    f'padding:7px 9px;flex:1 1 auto;overflow:hidden;cursor:pointer}}'
    f'.lesson.hidden{{opacity:.32}}'
    f'.lesson-title{{font-size:13px;font-weight:600;line-height:1.25;color:#f3f3f8}}'
    f'.lesson-prof{{font-size:12px;margin-top:3px;color:rgba(255,255,255,.62)}}'
    f'.lesson-room{{font-size:12px;margin-top:2px;color:rgba(255,255,255,.45)}}'
    f'.day-cell{{content-visibility:auto;contain-intrinsic-size:92px}}'
)

DC_RULE_PRETTY = "\n        [id^=\"day-\"] { display: contents; }"
DC_RULE_MINI   = '[id^="day-"]{display:contents}'

# Same JS as Get.cshtml (whitespace-collapsed; constant across all variants).
JS = (
    'requestAnimationFrame(()=>{const cur=document.getElementById("day-2025-10-06");'
    'if(cur)(cur.firstElementChild??cur).scrollIntoView({block:"center"});});'
    "document.querySelector('.grid').addEventListener('click',e=>{"
    "const lesson=e.target.closest('.lesson');if(lesson){toggleLesson(lesson);return;}"
    "const label=e.target.closest('.day-label');if(!label)return;"
    "const id=label.closest('[id^=\"day-\"]')?.id;if(id)refreshDay(id.slice(4));});"
    "async function toggleLesson(el){const dateStr=el.closest('[id^=\"day-\"]')?.id.slice(4);"
    "const num=el.dataset.num;if(!dateStr||!num)return;"
    "const action=el.classList.contains('hidden')?'Unhide':'Hide';"
    "try{const res=await fetch('/App/'+action+'?date='+dateStr+'&lessonNumber='+num,{method:'PATCH'});"
    "if(res.ok)swapDay(dateStr,await res.text());}catch{}}"
    "async function refreshDay(dateStr){try{const res=await fetch('/App/GetOne?date='+dateStr);"
    "if(res.ok)swapDay(dateStr,await res.text());}catch{}}"
    "function swapDay(dateStr,html){const wrapper=document.getElementById('day-'+dateStr);"
    "if(!wrapper)return;const tmp=document.createElement('div');tmp.innerHTML=html;"
    "const next=tmp.querySelector('[id=\"day-'+dateStr+'\"]');if(next)wrapper.replaceWith(next);}"
    "if('serviceWorker'in navigator)navigator.serviceWorker.register('/sw.js');"
)

# ── HTML generator (one place; parameterised by every tweak) ─────────────────────
def gen(skip_empty: bool, css_rule: bool, minify: bool, title_fn) -> bytes:
    base_css = CSS_MINI if minify else CSS_PRETTY
    css      = base_css + (DC_RULE_MINI if minify else DC_RULE_PRETTY) if css_rule else base_css
    nl       = "" if minify else "\n"

    p = ['<!DOCTYPE html>\n<html lang="ru">\n<head>\n'
         '<meta charset="utf-8"/>\n'
         '<meta name="viewport" content="width=device-width,initial-scale=1"/>\n'
         '<title>Расписание</title>\n<style>%s</style>\n</head>\n<body>\n'
         '<div id="scroller"><div class="grid">\n'
         '<div class="cell s-both"></div>\n' % css]

    for num in slots:
        p.append(f'<div class="cell s-top slot-head"><div class="slot-start">Пара {num}</div></div>{nl}')

    for day in data["grid"]:
        ds       = day["date"]
        dlessons = day_map.get(ds, {})
        if skip_empty and not dlessons:
            continue
        y, m, d_num = map(int, ds.split("-"))
        dt = date(y, m, d_num)
        dc_attr = "" if css_rule else ' style="display:contents"'
        p.append(f'<div id="day-{ds}"{dc_attr}>{nl}')
        p.append(f'<div class="cell s-left day-label">'
                 f'<div class="day-name">{WEEKDAYS[dt.weekday()]}</div>'
                 f'<div class="day-date">{dt.day}.{dt.month}</div></div>{nl}')
        for num in slots:
            lessons = dlessons.get(num)
            if lessons:
                p.append('<div class="cell day-cell">')
                for l in lessons:
                    title = title_fn(l["title_raw"])
                    p.append(
                        f'<div class="lesson" data-num="{num}"'
                        f' style="--accent:{accent(l["type"])};--accent-bg:{accent_bg(l["type"])}">'
                        f'<div class="lesson-title">{title}</div>'
                        + (f'<div class="lesson-room">{l["room"]}</div>' if l["room"] else "")
                        + '</div>')
                p.append(f'</div>{nl}')
            else:
                p.append(f'<div class="cell day-cell"></div>{nl}')
        p.append(f'</div>{nl}')

    p.append(f'</div></div>\n<script>{JS}</script>\n</body>\n</html>')
    return "".join(p).encode("utf-8")

TITLE_FNS = {
    "abbrev": lambda t: ABBREVS.get(t, t),   # current behaviour
    "full":   lambda t: t,                   # full subject names
}

# ── build every variant into a temp dir ─────────────────────────────────────────
combos = list(itertools.product([False, True], repeat=3))  # skip, css_rule, minify
tmp = tempfile.mkdtemp(prefix="viewbench_")
manifest = []  # (key, skip, css_rule, minify, title_set)
try:
    for tname, fn in TITLE_FNS.items():
        for i, (skip, css_rule, minify) in enumerate(combos):
            key = f"{tname}_{i}"
            with open(os.path.join(tmp, key + ".html"), "wb") as fh:
                fh.write(gen(skip, css_rule, minify, fn))
            manifest.append((key, skip, css_rule, minify, tname))

    # ── compress everything via the repo's real .NET path, one process ──────────
    proc = subprocess.run(
        ["dotnet", "run", BROTLI_NET, "--", "--dir", tmp],
        capture_output=True, text=True, cwd=BASE)
    if proc.returncode != 0:
        sys.stderr.write(proc.stdout + "\n" + proc.stderr + "\n")
        sys.exit("brotli_net.cs failed")

    sizes = {}  # key -> (optimal, smallest, fastest, raw)
    for line in proc.stdout.splitlines():
        if not line.strip(): continue
        name, opt, sml, fst, raw = line.split("\t")
        sizes[name] = (int(opt), int(sml), int(fst), int(raw))
finally:
    shutil.rmtree(tmp, ignore_errors=True)

# ── report ──────────────────────────────────────────────────────────────────────
total_days    = len(data["grid"])
days_nonempty = sum(1 for ds in day_map if day_map[ds])
print(f"Days total: {total_days}, with lessons: {days_nonempty}, "
      f"empty: {total_days - days_nonempty}   Slots: {slots}")
print("Compression: .NET BrotliStream — Optimal = what the repo ships (== brotli q4); "
      "Smallest = q11 (for reference).\n")

def yn(b): return "yes" if b else "no"

for tname in TITLE_FNS:
    rows = [(k, sk, cs, mi) for (k, sk, cs, mi, t) in manifest if t == tname]
    best_opt = min(sizes[k][0] for k, *_ in rows)
    print(f"=== titles: {tname} ===")
    print(f"{'skip':<5}{'css-rule':<9}{'minify':<7}"
          f"{'Optimal(br)':>12}{'Δ vs best':>11}{'Smallest':>10}{'Fastest':>9}{'raw':>9}")
    print("-" * 72)
    for k, sk, cs, mi in sorted(rows, key=lambda r: sizes[r[0]][0]):
        opt, sml, fst, raw = sizes[k]
        mark = "  <= best" if opt == best_opt else ""
        print(f"{yn(sk):<5}{yn(cs):<9}{yn(mi):<7}"
              f"{opt:>12}{opt-best_opt:>+11}{sml:>10}{fst:>9}{raw:>9}{mark}")
    print()

# ── does the winner change between Optimal (shipped) and Smallest (q11)? ─────────
print("=== winner stability: Optimal (shipped) vs Smallest (q11) ===")
for tname in TITLE_FNS:
    rows = [k for (k, *_ , t) in manifest if t == tname]
    win_opt = min(rows, key=lambda k: sizes[k][0])
    win_sml = min(rows, key=lambda k: sizes[k][1])
    def desc(k):
        _, sk, cs, mi, _ = next(m for m in manifest if m[0] == k)
        return f"skip={yn(sk)},css-rule={yn(cs)},minify={yn(mi)}"
    same = "same" if win_opt == win_sml else "DIFFERENT"
    print(f"  {tname:<7} Optimal-> {desc(win_opt):<34} | q11-> {desc(win_sml):<34} [{same}]")
