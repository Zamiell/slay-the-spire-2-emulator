import sys

with open("src/sts2_gym/run_env.py", "r") as f:
    lines = f.readlines()

new_lines = []
seen_defs = set()
in_duplicate_range = False

# We want to keep the FIRST occurrence of each def, and remove subsequent ones.
# But wait, we just added the CORRECT ones at the end?
# No, we replaced them in place earlier.

# Actually, I'll just find where the first 'def _combat_deck' starts and remove everything after it
# IF there is another 'def _combat_deck' later.

# Better: just use a simple state machine to keep only the FIRST definition of each method.
# But methods can be multi-line.


def get_def_name(line):
    line = line.strip()
    if line.startswith("def "):
        return line.split("(")[0].split("def ")[1].strip()
    return None


def clean_file():
    final_lines = []
    seen_methods = {}  # name -> list of lines

    current_method_name = None
    current_method_lines = []

    header_lines = []

    for line in lines:
        name = get_def_name(line)
        if name:
            if current_method_name:
                # Save previous method
                if current_method_name not in seen_methods:
                    seen_methods[current_method_name] = current_method_lines
                else:
                    # Duplicate! We'll keep the LATEST one because my recent edits were at the end.
                    seen_methods[current_method_name] = current_method_lines

            current_method_name = name
            current_method_lines = [line]
        elif current_method_name:
            current_method_lines.append(line)
        else:
            header_lines.append(line)

    if current_method_name:
        seen_methods[current_method_name] = current_method_lines

    # Reconstruct. Wait, I want to preserve the order!
    # I'll do another pass.

    final_lines = list(header_lines)
    re_seen = set()

    # Wait, if I want to keep the LATEST definition, I should reverse the file.

    method_order = []
    for line in lines:
        name = get_def_name(line)
        if name and name not in method_order:
            method_order.append(name)

    for name in method_order:
        final_lines.extend(seen_methods[name])

    with open("src/sts2_gym/run_env.py", "w") as f:
        f.writelines(final_lines)


clean_file()
