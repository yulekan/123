# -*- coding: utf-8 -*-
import re
import os

base = os.path.dirname(os.path.abspath(__file__))
for f in ('content_part1.py', 'content_part2.py'):
    print('=====', f)
    src = open(os.path.join(base, f), encoding='utf-8').read()
    for m in re.finditer(r"b\.(h1|h2|h3)\((.*)", src):
        print(m.group(1), m.group(2)[:120])
