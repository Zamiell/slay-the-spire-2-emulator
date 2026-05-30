import sys

with open('src/sts2_gym/run_env.py', 'r') as f:
    lines = f.readlines()

start_line = -1
end_line = -1

for i, line in enumerate(lines):
    if line.startswith('CARD_RARITY_BY_ID = {'):
        start_line = i
    if start_line != -1 and line.strip() == '}' and end_line == -1 and i > start_line:
        end_line = i
        break

if start_line != -1 and end_line != -1:
    with open('new_rarity_map.txt', 'r') as f:
        new_map = f.readlines()
    
    new_lines = lines[:start_line] + new_map + lines[end_line+1:]
    
    with open('src/sts2_gym/run_env.py', 'w') as f:
        f.writelines(new_lines)
    print(f"Successfully replaced CARD_RARITY_BY_ID from line {start_line+1} to {end_line+1}.")
else:
    print("Failed to find CARD_RARITY_BY_ID block.")
