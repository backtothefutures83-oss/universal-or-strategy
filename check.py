import sys, re

text = open('tests/ExecutionEngineIntegrationTests.cs', encoding='utf-8').read()
# Remove block comments
text = re.sub(r'/\*.*?\*/', '', text, flags=re.DOTALL)
# Remove line comments
text = re.sub(r'//.*', '', text)
# Remove strings
text = re.sub(r'"(\\.|[^\\"])*"', '', text)
# Remove char literals
text = re.sub(r'\'(\\.|[^\\\'])\'', '', text)

lines = text.split('\n')
level = 0
for i, line in enumerate(lines):
    level += line.count('{')
    level -= line.count('}')
    if level < 2 and i > 25:
        print(f'Line {i+1} drops below 2: level {level}')
        break
    if level < 1:
        print(f'Line {i+1} drops below 1: level {level}')
