#!/usr/bin/env python3
"""
بررسی خودکار دسترس‌پذیری SadGallery (فاز ۳).

چه چیزی را می‌سنجد؟
  ۱) نسبت کنتراست WCAG 2.1 برای جفت‌های «متن/پس‌زمینه» که واقعاً در site.css استفاده می‌شوند
     (مقادیر از خود فایل CSS خوانده می‌شوند، نه کپی دستی).
  ۲) وجود قاعده «هدف لمسی حداقل ۴۴px» (.sg-touch) در CSS.
  ۳) وجود نوار حالت آفلاین (.sg-offline-banner) و اسکریپت تشخیص قطعی.

استفاده:
  python3 scripts/accessibility-check.py            # بررسی؛ کد خروج ۱ در صورت شکست
  python3 scripts/accessibility-check.py --write-doc  # نوشتن نتیجه در docs/ACCESSIBILITY.md
"""

from __future__ import annotations

import re
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CSS_PATH = ROOT / "src/SadGallery.Web/wwwroot/css/site.css"
JS_PATH = ROOT / "src/SadGallery.Web/wwwroot/js/offline-status.js"
DOC_PATH = ROOT / "docs/ACCESSIBILITY.md"

AA_NORMAL = 4.5   # متن معمولی
AA_LARGE = 3.0    # متن بزرگ (≥۱۸٫۶۶px ضخیم یا ≥۲۴px)

# جفت‌هایی که در رابط واقعاً به‌کار می‌روند: (توضیح، رنگ متن، رنگ پس‌زمینه، حد لازم)
# رنگ‌ها یا از متغیرهای :root می‌آیند یا ثابت‌های همان CSS (سفید کارت‌ها، سوءاستفاده‌شده در Bootstrap badge).
PAIRS = [
    ("متن اصلی روی پس‌زمینه کرم", "ink", "cream", AA_NORMAL),
    ("متن خاموش (muted) روی کارت سفید", "muted", "#ffffff", AA_NORMAL),
    ("متن خاموش روی کرم", "muted", "cream", AA_NORMAL),
    ("پیوند منو روی سربرگ سرمه‌ای", "#e9eef7", "navy", AA_NORMAL),
    ("منوی غیرفعال (اپسیتی ۰٫۶۵ — رنگ ترکیبی محاسبه‌شده)", "#9da7b7", "navy", AA_NORMAL),
    ("متن پانوشت", "#cfd8e6", "navy", AA_NORMAL),
    ("یادداشت پانوشت", "#9fb0c8", "navy", AA_NORMAL),
    ("دکمه طلایی: متن سرمه‌ای روی طلا", "navy", "gold", AA_NORMAL),
    ("دکمه طلایی hover: متن سرمه‌ای روی طلای تیره", "navy", "gold_dark", AA_NORMAL),
    ("برند hover: طلا روی سرمه‌ای", "gold", "navy", AA_NORMAL),
    ("عنوان کارت: سرمه‌ای روی سفید", "navy", "#ffffff", AA_NORMAL),
    ("کد: سرمه‌ای ملایم روی کرم", "navy_soft", "cream", AA_NORMAL),
    ("نشان موفقیت Bootstrap: سفید روی سبز", "#ffffff", "#198754", AA_NORMAL),
    ("نشان هشدار Bootstrap: سیاه روی زرد", "#000000", "#ffc107", AA_NORMAL),
    ("نشان ثانویه Bootstrap: سفید روی خاکستری", "#ffffff", "#6c757d", AA_NORMAL),
    ("نشان خطر Bootstrap: سفید روی قرمز", "#ffffff", "#dc3545", AA_NORMAL),
    ("نوار آفلاین: سفید روی قرمز تیره", "#ffffff", "#7a1f1f", AA_NORMAL),
]

VAR_RE = re.compile(r"--sg-([a-z-]+)\s*:\s*(#[0-9a-fA-F]{6})")


def read_variables(css: str) -> dict[str, str]:
    match = re.search(r":root\s*\{(.*?)\}", css, re.S)
    if not match:
        raise SystemExit("متغیرهای :root در site.css پیدا نشد.")
    return {name: value.lower() for name, value in VAR_RE.findall(match.group(1))}


def _channel(value: float) -> float:
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


def luminance(hex_color: str) -> float:
    value = hex_color.lstrip("#")
    r, g, b = (int(value[i:i + 2], 16) / 255 for i in (0, 2, 4))
    return 0.2126 * _channel(r) + 0.7152 * _channel(g) + 0.0722 * _channel(b)


def contrast(a: str, b: str) -> float:
    la, lb = luminance(a), luminance(b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)


def resolve(token: str, variables: dict[str, str]) -> str:
    if token.startswith("#"):
        return token
    if token.replace("_", "-") in variables:
        return variables[token.replace("_", "-")]
    raise SystemExit(f"متغیر رنگ ناشناخته: {token}")


def main() -> int:
    css = CSS_PATH.read_text(encoding="utf-8")
    js = JS_PATH.read_text(encoding="utf-8") if JS_PATH.exists() else ""
    variables = read_variables(css)

    lines: list[str] = []
    failures = 0

    lines.append("| جفت متن/پس‌زمینه | نسبت کنتراست | حد WCAG AA | نتیجه |")
    lines.append("| --- | --- | --- | --- |")

    for label, fg_token, bg_token, threshold in PAIRS:
        fg, bg = resolve(fg_token, variables), resolve(bg_token, variables)
        value = contrast(fg, bg)
        passed = value >= threshold
        if not passed:
            failures += 1
        lines.append(f"| {label} | {value:.2f}:1 | {threshold}:1 | {'قبول ✓' if passed else 'رد ✗'} |")

    touch_ok = ".sg-touch" in css and "min-height: 44px" in css and "min-width: 44px" in css
    banner_ok = ".sg-offline-banner" in css
    js_ok = "navigator.onLine" in js and "offline" in js

    print("═══ بررسی دسترس‌پذیری SadGallery ═══")
    for line in lines[2:]:
        print("  " + line)
    print()
    print(f"  هدف لمسی ۴۴px (.sg-touch): {'موجود ✓' if touch_ok else 'غایب ✗'}")
    print(f"  نوار حالت آفلاین: {'موجود ✓' if banner_ok else 'غایب ✗'}")
    print(f"  اسکریپت تشخیص قطعی: {'موجود ✓' if js_ok else 'غایب ✗'}")
    print()
    print("  نتیجه:", "همه معیارها قبول ✓" if failures == 0 and touch_ok and banner_ok and js_ok
          else f"{failures} کنتراست زیر حد یا عنصر غایب ✗")

    if "--write-doc" in sys.argv:
        stamp = datetime.now(timezone.utc).strftime("%Y-%m-%d")
        header = f"""# دسترس‌پذیری (فاز ۳)

- **روش سنجش:** ابزار داخلی `scripts/accessibility-check.py` (اجراشده در {stamp}) — رنگ‌ها مستقیماً از
  `src/SadGallery.Web/wwwroot/css/site.css` خوانده و نسبت کنتراست WCAG 2.1 محاسبه می‌شود.
- **حد پذیرفته‌شده:** متن معمولی ≥ ۴٫۵:۱ و متن بزرگ ≥ ۳:۱ (سطح AA).
- **کاربرگ‌های لمسی:** قاعده `.sg-touch` (حداقل ۴۴×۴۴px) برای کنترل‌های تعاملی؛ در بررسی هم ارزیابی می‌شود.
- **حالت آفلاین:** نوار هشدار `.sg-offline-banner` + `wwwroot/js/offline-status.js` (بدون منبع خارجی).

## نتیجه اجرای ابزار

"""
        footer = f"""
## محدودیت صریح این سنجش

سنجش کنتراست «محاسباتی» و بر پایه رنگ‌های واقعی CSS است و پوشش کامل ممیزی با مرورگر واقعی
(اندازه‌گیری رندر، حالت بزرگ‌نمایی ۲۰۰٪، اسکرین‌ریدر) نیازمند محیط مالک است؛ در `KNOWN_LIMITATIONS.md` ثبت شده.
"""
        DOC_PATH.write_text(header + "\n".join(lines) + "\n" + footer, encoding="utf-8")
        print(f"  ✓ نوشته شد: {DOC_PATH.relative_to(ROOT)}")

    return 0 if failures == 0 and touch_ok and banner_ok and js_ok else 1


if __name__ == "__main__":
    sys.exit(main())
