#!/usr/bin/env python3
import json, itertools, subprocess, sys
from datetime import date

# ── fetch ──────────────────────────────────────────────────────────────────────
import os
_base = os.path.dirname(os.path.abspath(__file__))
with open(os.path.join(_base, "schedule_raw.json"), encoding="utf-8") as f:
    data = json.load(f)

# ── abbreviations (from GetOne.cshtml) ─────────────────────────────────────────
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

# ── helpers ────────────────────────────────────────────────────────────────────
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

# ── process raw data ───────────────────────────────────────────────────────────
day_map    = {}   # date_str -> {slot_num: lesson_dict}
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
        lesson_obj = {
            "title":     ABBREVS.get(raw_title, raw_title),
            "title_raw": raw_title,
            "prof":  prof((l.get("professor") or {}).get("fullName")),
            "room":  room(l.get("audience")),
            "type":  collapse(l.get("lessonType","")).upper(),
        }
        dmap.setdefault(num, []).append(lesson_obj)
        slots_used.add(num)
    day_map[ds] = dmap

slots = sorted(slots_used)
N     = len(slots)

# Inject a 2nd (custom) lesson into the first occupied slot to simulate real-world data
for ds, dmap in day_map.items():
    for num in list(dmap.keys()):
        if len(dmap[num]) == 1:
            custom = dict(dmap[num][0])
            custom["title"] = "Консультация"
            custom["title_raw"] = "Консультация"
            custom["type"] = "CONSULTATION"
            custom["room"] = "305"
            dmap[num].append(custom)
            break
    else:
        continue
    break

# ── CSS ────────────────────────────────────────────────────────────────────────
CSS_PRETTY = f"""\
        :root {{
            --bg: #0a0a0f;
            --line: #1b1b24;
            --fg: #e8e8ef;
            --sub: #6b6b7b;
            --row-min: 136px;
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
        .lesson-room  {{ font-size: 12px; margin-top: 2px; color: rgba(255,255,255,.45); }}"""

CSS_MINI = (
    f':root{{--bg:#0a0a0f;--line:#1b1b24;--fg:#e8e8ef;--sub:#6b6b7b;--row-min:136px;--day-w:58px}}'
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
)

DC_RULE_PRETTY = "\n        [id^=\"day-\"] { display: contents; }"
DC_RULE_MINI   = '[id^="day-"]{display:contents}'

JS = """\
        requestAnimationFrame(()=>{
            const cur=document.getElementById("day-2025-10-06");
            if(cur)(cur.firstElementChild??cur).scrollIntoView({block:"center"});
        });
        document.querySelector('.grid').addEventListener('click',e=>{
            const lesson=e.target.closest('.lesson');
            if(lesson){toggleLesson(lesson);return;}
            const label=e.target.closest('.day-label');
            if(!label)return;
            const id=label.closest('[id^="day-"]')?.id;
            if(id)refreshDay(id.slice(4));
        });
        async function toggleLesson(el){
            const dateStr=el.closest('[id^="day-"]')?.id.slice(4);
            const num=el.dataset.num;
            if(!dateStr||!num)return;
            const action=el.classList.contains('hidden')?'Unhide':'Hide';
            try{
                const res=await fetch('/App/'+action+'?date='+dateStr+'&lessonNumber='+num,{method:'PATCH'});
                if(res.ok)swapDay(dateStr,await res.text());
            }catch{}
        }
        async function refreshDay(dateStr){
            try{
                const res=await fetch('/App/GetOne?date='+dateStr);
                if(res.ok)swapDay(dateStr,await res.text());
            }catch{}
        }
        function swapDay(dateStr,html){
            const wrapper=document.getElementById('day-'+dateStr);
            if(!wrapper)return;
            const tmp=document.createElement('div');
            tmp.innerHTML=html;
            const next=tmp.querySelector('[id="day-'+dateStr+'"]');
            if(next)wrapper.replaceWith(next);
        }
        if('serviceWorker'in navigator)navigator.serviceWorker.register('/sw.js');"""

# ── HTML generator ─────────────────────────────────────────────────────────────
def gen(skip_empty: bool, css_class: bool, minify: bool) -> bytes:
    base_css = CSS_MINI if minify else CSS_PRETTY
    dc_rule  = DC_RULE_MINI if minify else DC_RULE_PRETTY
    css      = base_css + dc_rule if css_class else base_css

    p = []
    p.append(f'<!DOCTYPE html>\n<html lang="ru">\n<head>\n'
             f'<meta charset="utf-8"/>\n'
             f'<meta name="viewport" content="width=device-width,initial-scale=1"/>\n'
             f'<title>Расписание</title>\n<style>{css}</style>\n</head>\n<body>\n'
             f'<div id="scroller"><div class="grid">\n'
             f'<div class="cell s-both"></div>\n')

    for num in slots:
        p.append(f'<div class="cell s-top slot-head"><div class="slot-start">Пара {num}</div></div>\n')

    for day in data["grid"]:
        ds      = day["date"]
        dlessons = day_map.get(ds, {})
        if skip_empty and not dlessons:
            continue

        y, m, d_num = map(int, ds.split("-"))
        dt  = date(y, m, d_num)
        dow = dt.weekday()

        dc_attr = "" if css_class else ' style="display:contents"'
        p.append(f'<div id="day-{ds}"{dc_attr}>\n')
        p.append(f'<div class="cell s-left day-label">'
                 f'<div class="day-name">{WEEKDAYS[dow]}</div>'
                 f'<div class="day-date">{dt.day}.{dt.month}</div></div>\n')

        for num in slots:
            lessons = dlessons.get(num)
            if lessons:
                p.append(f'<div class="cell day-cell">')
                for l in lessons:
                    p.append(
                        f'<div class="lesson" data-num="{num}"'
                        f' style="--accent:{accent(l["type"])};--accent-bg:{accent_bg(l["type"])}">'
                        f'<div class="lesson-title">{l["title"]}</div>'
                        + (f'<div class="lesson-room">{l["room"]}</div>' if l["room"] else "")
                        + f'</div>'
                    )
                p.append('</div>\n')
            else:
                p.append(f'<div class="cell day-cell"></div>\n')

        p.append('</div>\n')

    p.append(f'</div></div>\n<script>{JS}</script>\n</body>\n</html>')
    return "".join(p).encode("utf-8")

# ── compress with brotli CLI ───────────────────────────────────────────────────
BROTLI = r"C:\Program Files\Git\mingw64\bin\brotli.exe"

def brotli_size(html: bytes) -> int:
    r = subprocess.run([BROTLI, "-q", "11", "--stdout"], input=html,
                       capture_output=True)
    return len(r.stdout)

# ── run all 8 ─────────────────────────────────────────────────────────────────
total_days    = len(data["grid"])
days_nonempty = sum(1 for ds in day_map if day_map[ds])

print(f"Days total: {total_days},  with lessons: {days_nonempty},  empty: {total_days-days_nonempty}")
print(f"Slots: {slots}\n")


def gen_titled(skip: bool, css_cls: bool, minify: bool, title_fn) -> bytes:
    base_css = CSS_MINI if minify else CSS_PRETTY
    dc_rule  = DC_RULE_MINI if minify else DC_RULE_PRETTY
    css      = base_css + dc_rule if css_cls else base_css

    p = []
    p.append(f'<!DOCTYPE html>\n<html lang="ru">\n<head>\n'
             f'<meta charset="utf-8"/>\n'
             f'<meta name="viewport" content="width=device-width,initial-scale=1"/>\n'
             f'<title>Расписание</title>\n<style>{css}</style>\n</head>\n<body>\n'
             f'<div id="scroller"><div class="grid">\n'
             f'<div class="cell s-both"></div>\n')
    for num in slots:
        p.append(f'<div class="cell s-top slot-head"><div class="slot-start">Пара {num}</div></div>\n')

    for day in data["grid"]:
        ds       = day["date"]
        dlessons = day_map.get(ds, {})
        if skip and not dlessons:
            continue
        y, m, d_num = map(int, ds.split("-"))
        dt  = date(y, m, d_num)
        dc_attr = "" if css_cls else ' style="display:contents"'
        p.append(f'<div id="day-{ds}"{dc_attr}>\n')
        p.append(f'<div class="cell s-left day-label">'
                 f'<div class="day-name">{WEEKDAYS[dt.weekday()]}</div>'
                 f'<div class="day-date">{dt.day}.{dt.month}</div></div>\n')
        for num in slots:
            lessons = dlessons.get(num)
            if lessons:
                p.append(f'<div class="cell day-cell">')
                for l in lessons:
                    title = title_fn(l["title_raw"])
                    p.append(
                        f'<div class="lesson" data-num="{num}"'
                        f' style="--accent:{accent(l["type"])};--accent-bg:{accent_bg(l["type"])}">'
                        f'<div class="lesson-title">{title}</div>'
                        + (f'<div class="lesson-room">{l["room"]}</div>' if l["room"] else "")
                        + f'</div>'
                    )
                p.append('</div>\n')
            else:
                p.append(f'<div class="cell day-cell"></div>\n')
        p.append('</div>\n')

    p.append(f'</div></div>\n<script>{JS}</script>\n</body>\n</html>')
    return "".join(p).encode("utf-8")

TITLE_FNS = {
    "abbrev": lambda t: ABBREVS.get(t, t),
    "full":   lambda t: t,
    "2xfull": lambda t: t + " " + t,
}

combos = list(itertools.product([False, True], repeat=3))

# collect all sizes: results[title_set][combo_index] = brotli_size
results = {name: [] for name in TITLE_FNS}
for name, fn in TITLE_FNS.items():
    for skip, css_cls, minify in combos:
        html = gen_titled(skip, css_cls, minify, fn)
        results[name].append(brotli_size(html))

# rank within each title set (0 = smallest)
def ranks(sizes):
    order = sorted(range(len(sizes)), key=lambda i: sizes[i])
    r = [0] * len(sizes)
    for rank, idx in enumerate(order):
        r[idx] = rank
    return r

ranks_by_set = {name: ranks(results[name]) for name in TITLE_FNS}

print(f"{'#':>2}  skip css  min  {'abbrev':>7}  {'full':>7}  {'2xfull':>7}  rank stable?")
print("-" * 62)
for i, (skip, css, mini) in enumerate(combos):
    s = "yes" if skip else "no"
    c = "yes" if css  else "no"
    m = "yes" if mini else "no"
    ra = ranks_by_set["abbrev"][i]
    rb = ranks_by_set["full"][i]
    rc = ranks_by_set["2xfull"][i]
    stable = (ra == rb == rc)
    print(f"{i:>2}  {s:<4} {c:<4} {m:<4} "
          f"  {results['abbrev'][i]:>6}  {results['full'][i]:>6}  {results['2xfull'][i]:>7}"
          f"  {'YES' if stable else 'NO !'}")